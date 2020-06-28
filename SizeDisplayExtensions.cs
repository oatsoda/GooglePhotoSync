using System;

namespace GooglePhotoSync
{
    public static class SizeDisplayExtensions
    {
        public static string AsHumanReadableBytes(this long bytes, string unit)
        {
            if (unit != "KB" && unit != "MB" && unit != "GB")
                throw new ArgumentOutOfRangeException(nameof(unit), "Must be 'KB', 'MB' or 'GB");

            var converted = bytes / 1024;
            if (unit == "MB")
                converted /= 1024;
            if (unit == "GB")
                converted /= 1024;

            return $"{converted} {unit}";
        }
    }
}