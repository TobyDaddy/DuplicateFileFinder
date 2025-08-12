using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.ComponentModel;
using System.Linq;

public static class FileUtils
{
    public static Tuple<List<List<string>>, int> FindDuplicates(string folderPath, BackgroundWorker worker)
    {
        var duplicates = new List<List<string>>();
        var filesBySize = new Dictionary<long, List<string>>();

        var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories).ToList();
        int totalFiles = files.Count;

        // 第一阶段：按大小分组（0-30%）
        for (int i = 0; i < totalFiles; i++)
        {
            if (worker.CancellationPending)
            {
                return new Tuple<List<List<string>>, int>(duplicates, totalFiles);
            }

            var file = files[i];
            var fileInfo = new FileInfo(file);
            if (!filesBySize.ContainsKey(fileInfo.Length))
            {
                filesBySize[fileInfo.Length] = new List<string>();
            }
            filesBySize[fileInfo.Length].Add(file);

            int progress = (int)((i + 1) / (float)totalFiles * 30);
            worker.ReportProgress(progress);
        }

        // 第二阶段：哈希计算（30-100%）
        var groupsToCheck = filesBySize.Values.Where(group => group.Count > 1).ToList();
        int totalGroupsToCheck = groupsToCheck.Sum(g => g.Count);
        int processedFiles = 0;

        foreach (var group in groupsToCheck)
        {
            if (worker.CancellationPending)
            {
                return new Tuple<List<List<string>>, int>(duplicates, totalFiles);
            }

            var hashes = new Dictionary<string, List<string>>();
            
            foreach (var filePath in group)
            {
                if (worker.CancellationPending)
                {
                    return new Tuple<List<List<string>>, int>(duplicates, totalFiles);
                }

                string fileHash = GetFileHash(filePath);
                if (hashes.ContainsKey(fileHash))
                {
                    hashes[fileHash].Add(filePath);
                }
                else
                {
                    hashes[fileHash] = new List<string> { filePath };
                }

                processedFiles++;
                int progress = 30 + (int)((processedFiles / (float)totalGroupsToCheck) * 70);
                worker.ReportProgress(Math.Min(progress, 100));
            }

            foreach (var hashGroup in hashes.Values)
            {
                if (hashGroup.Count > 1)
                {
                    duplicates.Add(hashGroup);
                }
            }
        }

        worker.ReportProgress(100);
        return new Tuple<List<List<string>>, int>(duplicates, totalFiles);
    }

    public static string GetFileHash(string filePath)
    {
        const int bufferSize = 512;

        using (var md5 = MD5.Create())
        using (var stream = File.OpenRead(filePath))
        {
            var buffer = new byte[bufferSize * 2];

            int bytesReadStart = stream.Read(buffer, 0, bufferSize);

            if (stream.Length > bufferSize)
            {
                stream.Seek(-bufferSize, SeekOrigin.End);
                int bytesReadEnd = stream.Read(buffer, bufferSize, bufferSize);

                return BitConverter.ToString(md5.ComputeHash(buffer, 0, bytesReadStart + bytesReadEnd)).Replace("-", "").ToLowerInvariant();
            }
            else
            {
                return BitConverter.ToString(md5.ComputeHash(buffer, 0, bytesReadStart)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
