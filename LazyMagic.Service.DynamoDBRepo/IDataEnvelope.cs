using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace LazyMagic.Service.DynamoDBRepo;

/// <summary>
/// The Envelope is the record we store in the database.
/// Because DynamoDB is a NoSQL database and does not provide 
/// sophisticated indexing like a SQL database, we implement
/// a ISAM style indexing strategy.
/// See the LazyMagic.io documentation for more information on
/// how to map your ER into a DynamoDB table using this 
/// Envelope.
/// </summary>
public interface IDataEnvelope<T>
    where T : class, IItem, new()
{
    static string DefaultPK { get; }
    string CurrentTypeName { get; set; }

    // Data Entity stored/retrieved in DynamoDB record
    Dictionary<string, AttributeValue> DbRecord { get; set; }

    // Data Entity - in latest version form!
    T EntityInstance { get; set; }

    /// <summary>
    /// Type read from dbRecord 
    /// </summary>
    string TypeName { get; set; }

    string PayloadId { get; set; }  

    /// <summary>
    /// Partition Key
    /// </summary>
    string PK { get; set; }

    /// <summary>
    /// Sort Key. Also called Range Key
    /// </summary>
    string SK { get; set; }

    /// <summary>
    /// for LSI PK-SK1-Index
    /// </summary>
    string SK1 { get; set; }

    /// <summary>
    /// for LSI PK-SK2-Index
    /// </summary>
    string SK2 { get; set; }

    /// <summary>
    /// for LSI PK-SK3-Index
    /// </summary>
    string SK3 { get; set; }

    /// <summary>
    /// for LSI PK-SK4-Index
    /// </summary>
    string SK4 { get; set; }

    /// <summary>
    /// for LSI PK-SK5-Index
    /// </summary>
    string SK5 { get; set; }

    // Global Secondary Indices

    /// <summary>
    /// partition key for GSI1
    /// </summary>
    string GSI1PK { get; set; }

    /// <summary>
    /// sort key for GSI1
    /// </summary>
    string GSI1SK { get; set; }

    /// <summary>
    /// Projection attribute
    /// </summary>
    string Status { get; set; }

    /// <summary>
    ///  Projection attribute
    /// </summary>
    long UpdateUtcTick { get; set; }

    /// <summary>
    /// Projection attribute
    /// </summary>
    long CreateUtcTick { get; set; }

    /// <summary>
    /// Projection attribute - kitchen sink
    /// </summary>
    string General { get; set; }
    /// <summary>
    /// Add a TTL attribute to the record so DynamoDB can remove the record.
    /// </summary>
    bool UseTTL { get; set; }
    /// <summary>
    ///  Number of seconds to add to current time for "deletion" of records with a TTL attribute
    /// </summary>
    long TTLPeriod { get; set; }
    /// <summary>
    /// Boolean flag indicating record is marked for deletion. Generally, when we mark a record 
    /// for deletion, we also set the TTL so DynamoDB will remove the record. We us the IsDeleted
    /// approach to allow for Notification records created from the stream to contain a 
    /// SessionId value of the client session that delted the record.
    /// </summary>
    bool IsDeleted { get; set; }
    /// <summary>
    /// The client sessionId that performed the latest operation on the record. This is used 
    /// by the Notifications process to avoid sending a notification to the client that 
    /// made the change. 
    /// </summary>
    string SessionId { get; set; }
    int JsonSize { get; }

    public void SealEnvelope();
}
