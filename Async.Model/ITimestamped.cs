using System;

namespace Async.Model
{
    public interface ITimestamped
    {
        DateTime LastUpdated { get; }
    }
}
