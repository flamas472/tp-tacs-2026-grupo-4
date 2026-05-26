using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

public class MongoIdGenerator : IIdGenerator
{
    private readonly IMongoCollection<SequenceCounter> _counters;

    public MongoIdGenerator(MongoDbContext context)
    {
        _counters = context.Collection<SequenceCounter>("__entity_sequences");
    }

    public int GetNextId<T>() => GetNextId(typeof(T).Name);

    public int GetNextId(string sequenceName)
    {
        var filter = Builders<SequenceCounter>.Filter.Eq(c => c.Id, sequenceName);
        var update = Builders<SequenceCounter>.Update.Inc(c => c.SequenceValue, 1);
        var options = new FindOneAndUpdateOptions<SequenceCounter>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var counter = _counters.FindOneAndUpdate(filter, update, options);
        return counter.SequenceValue;
    }

    private class SequenceCounter
    {
        [BsonId]
        public string Id { get; set; } = null!;
        public int SequenceValue { get; set; }
    }
}
