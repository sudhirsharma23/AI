using System;

namespace TextractProcessor.Models
{
    /// <summary>
    /// Configuration for S3 document paths with date-based folder structure
    /// </summary>
    public class S3DocumentConfig
 {
        /// <summary>
        /// S3 bucket name
        /// </summary>
    public string BucketName { get; set; }

        /// <summary>
        /// Base folder name (e.g., "uploads")
        /// </summary>
   public string BaseFolder { get; set; } = "uploads";

   /// <summary>
     /// Date folder in YYYY-MM-DD format
        /// </summary>
        public string DateFolder { get; set; }

        /// <summary>
        /// List of file names to process
        /// </summary>
     public string[] FileNames { get; set; }

        /// <summary>
        /// Construct full S3 key for a file
        /// Format: {BaseFolder}/{DateFolder}/{FileNameWithoutExt}/{FileName}
    /// </summary>
        public string GetS3Key(string fileName)
      {
            var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
  return $"{BaseFolder}/{DateFolder}/{fileNameWithoutExt}/{fileName}";
    }

  /// <summary>
      /// Get all S3 keys for configured files
    /// </summary>
        public string[] GetAllS3Keys()
        {
            var keys = new string[FileNames.Length];
            for (int i = 0; i < FileNames.Length; i++)
            {
      keys[i] = GetS3Key(FileNames[i]);
     }
            return keys;
        }

        /// <summary>
        /// Create config for today's date
        /// </summary>
  public static S3DocumentConfig ForToday(string bucketName, params string[] fileNames)
        {
      return new S3DocumentConfig
    {
    BucketName = bucketName,
      DateFolder = DateTime.Now.ToString("yyyy-MM-dd"),
   FileNames = fileNames
        };
        }

        /// <summary>
        /// Create config for a specific date
        /// </summary>
        public static S3DocumentConfig ForDate(string bucketName, string date, params string[] fileNames)
        {
            return new S3DocumentConfig
            {
    BucketName = bucketName,
          DateFolder = date,
   FileNames = fileNames
          };
        }

        /// <summary>
      /// Create config for a specific DateTime
        /// </summary>
        public static S3DocumentConfig ForDateTime(string bucketName, DateTime date, params string[] fileNames)
  {
         return new S3DocumentConfig
          {
        BucketName = bucketName,
     DateFolder = date.ToString("yyyy-MM-dd"),
        FileNames = fileNames
   };
     }
    }
}
