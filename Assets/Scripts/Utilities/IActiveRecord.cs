namespace Plugins.Utilities
{
    public interface IActiveRecord : ISerializableRecord { }

    public interface IActiveRecord<T> : IActiveRecord
    {
        T Value { get; set; }
    }
}