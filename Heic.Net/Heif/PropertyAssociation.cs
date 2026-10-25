namespace Heic.Net.Heif
{
    internal readonly struct PropertyAssociation(int index, bool essential)
    {
        public int Index { get; } = index;
        public bool Essential { get; } = essential;
    }
}
