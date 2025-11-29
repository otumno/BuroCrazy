namespace Utilities
{
    public interface ISerializableRecord
    {
        bool IsChanged { get; }

        string Serialize();

        void Deserialize(string value);
    }
}