using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LazyMagic.Service.DynamoDBRepo;
public class PartialContentObjectResult<T> : ObjectResult
{
    private readonly bool _hasMore;

    public PartialContentObjectResult(IEnumerable<T> data, bool hasMore) : base(data)
    {
        _hasMore = hasMore;
        StatusCode = _hasMore ? (int)HttpStatusCode.PartialContent : (int)HttpStatusCode.OK;
    }
}

