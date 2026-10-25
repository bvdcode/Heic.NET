namespace Heic.Net.Heif
{
    internal readonly struct ItemExtent(ulong offset, ulong length)
    {
        public ulong Offset { get; } = offset;
        public ulong Length { get; } = length;
    }
}
