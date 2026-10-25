using System;

namespace Heic.Net
{
    /// <summary>
    /// Defines immutable resource limits for input files, image dimensions and item relationships.
    /// </summary>
    public class HeicDecodeOptions
    {
        /// <summary>
        /// Gets the default decoding limits.
        /// </summary>
        public static HeicDecodeOptions Default { get; } = new HeicDecodeOptions();

        /// <summary>
        /// Initializes a set of decoding limits.
        /// </summary>
        /// <param name="maxInputBytes">The maximum encoded input size in bytes; at least 32.</param>
        /// <param name="maxPixels">The maximum number of visible pixels in one image.</param>
        /// <param name="maxItems">The maximum number of HEIF items in one document.</param>
        /// <param name="maxDerivationDepth">The maximum nesting depth of derived images.</param>
        /// <exception cref="ArgumentOutOfRangeException">A resource limit is invalid.</exception>
        public HeicDecodeOptions(int maxInputBytes = 64 * 1024 * 1024, int maxPixels = 16 * 1024 * 1024,
            int maxItems = 1024, int maxDerivationDepth = 16)
        {
            if (maxInputBytes < 32 || maxPixels <= 0 || maxItems <= 0 || maxDerivationDepth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxInputBytes), "All resource limits must be positive.");
            }

            MaxInputBytes = maxInputBytes;
            MaxPixels = maxPixels;
            MaxItems = maxItems;
            MaxDerivationDepth = maxDerivationDepth;
        }

        /// <summary>
        /// Gets the maximum encoded input size in bytes.
        /// </summary>
        public int MaxInputBytes { get; }

        /// <summary>
        /// Gets the maximum number of visible pixels in one image.
        /// </summary>
        public int MaxPixels { get; }

        /// <summary>
        /// Gets the maximum number of HEIF items in one document.
        /// </summary>
        public int MaxItems { get; }

        /// <summary>
        /// Gets the maximum nesting depth of derived images.
        /// </summary>
        public int MaxDerivationDepth { get; }
    }
}
