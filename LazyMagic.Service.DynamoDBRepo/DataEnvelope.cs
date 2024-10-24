namespace LazyMagic.Service.DynamoDBRepo;

public abstract class DataEnvelope<T> : IDataEnvelope<T>
    where T : class, IItem, new()
{

    private Dictionary<string, AttributeValue> _dbRecord;
    public Dictionary<string, AttributeValue> DbRecord
    {
        get { return _dbRecord; }
        set
        {
            _dbRecord = value;
            OpenEnvelope();
        }
    }
    public virtual string DefaultPK { get; } = $"{nameof(T)}:"; 
    public virtual string CurrentTypeName { get; set; } = $"{nameof(T)}:v1.0.0";

    /// <summary>
    /// Set this value to false if you are not using Utc for optimistic locking
    /// This will avoid the small reflection overhead of checking for default 
    /// UpdateUtcTick and CreateUtcTick fields in your data entity type.
    /// </summary>
    protected bool defaultUtcHandling = true;
    /// <summary>
    /// You can use another name for CreateUtcTick in your data entity - assign that name
    /// in the derived type's constructor.
    /// </summary>
    protected static string propNameCreateUtcTick = "CreateUtcTick";
    /// <summary>
    /// You can use another name for UpdateUtcTick in your data entity - assign that name
    /// in the derived type's constructor,.
    /// </summary>
    protected static string propNameUpdateUtcTick = "UpdateUtcTick";

    public string PayloadId { get; set; }   = string.Empty; 

    private T _entityInstance;
    public T EntityInstance
    {
        get { return _entityInstance; }
        set
        {
            _entityInstance = value;
        }
    } // Data entity in latest version form

    public string TypeName { get; set; } // name of class serialized into the Data string

    public string PK { get; set; }

    public string SK { get; set; }

    // Note: DO NOT add annotations to the optional attributes below
    // Doing so will cause an error in the DynamoDB library.
    // Instead, you must declare local secondary index range keys and Global secondary index keys
    // when you create the table using the SAM template.

    public string SK1 { get; set; } = null;

    public string SK2 { get; set; } = null;

    public string SK3 { get; set; } = null;

    public string SK4 { get; set; } = null;

    public string SK5 { get; set; } = null;

    public string GSI1PK { get; set; } = null;

    public string GSI1SK { get; set; } = null;

    public string Status { get; set; } = null; // Projection attribute
    public bool UseTTL { get; set; } = false;
    public long TTLPeriod { get; set; } = 172800; // 48 hours default. 

    public int JsonSize { get; private set; }
    public bool IsDeleted { get; set; } // Used for soft delete
    public string SessionId { get; set; }
    public long CreateUtcTick
    {
        get { return GetCreateUtcTick(); }
        set
        {
            SetCreateUtcTick(value);
            if (DbRecord != null && DbRecord.ContainsKey("CreateUtcTick"))
                DbRecord["CreateUtcTick"].N = value.ToString();
        }
    } // Projection attribute

    public long UpdateUtcTick
    {
        get { return GetUpdateUtcTick(); }
        set
        {
            SetUpdateUtcTick(value);
            if (DbRecord != null && DbRecord.ContainsKey("UpdateUtcTick"))
                DbRecord["UpdateUtcTick"].N = value.ToString();
        }
    } // Projection attribute

    public string General { get; set; } = null; // Projection attribute

    /// <summary>
    /// Gets the CreateUtcTick value from the data entity
    /// If you don't want to suffer the impact of reflection you can override the  
    /// GetCreateUtcTick method (don't call the base method if you do)
    /// and get the CreateUtcTick (or similar) field directly.
    /// </summary>
    /// <returns></returns>
    private System.Reflection.PropertyInfo propInfoCreateUtcTick = typeof(T).GetProperty(propNameCreateUtcTick);

    protected virtual long GetCreateUtcTick()
    {
        if (defaultUtcHandling && EntityInstance != null && propInfoCreateUtcTick != null)
            return (long)propInfoCreateUtcTick.GetValue(EntityInstance);
        return 0;
    }

    /// <summary>
    /// Gets the UpdateUtcTick value from the data entity
    /// If you don't want to suffer the impact of reflection you can override the 
    /// GetUpdateUtcTick method (don't call the base method if you do)
    /// and get the UpdateUtcTick (or similar) field directly.
    /// </summary>
    /// <returns></returns>
    private System.Reflection.PropertyInfo propInfoUpdateUtcTick = typeof(T).GetProperty(propNameUpdateUtcTick);

    protected virtual long GetUpdateUtcTick()
    {
        if (defaultUtcHandling && EntityInstance != null && propInfoUpdateUtcTick != null)
            return (long)propInfoUpdateUtcTick.GetValue(EntityInstance);
        return 0;
    }

    /// <summary>
    /// Sets the CreateUtcTick value into the data entity
    /// If you don't want to suffer the impact of reflection you can override the 
    /// SetCreateUtcTick method (don't call the base method if you do)
    /// and set the CreateUtcTick (or similar) field directly.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    protected virtual long SetCreateUtcTick(long value)
    {
        if (defaultUtcHandling && EntityInstance != null && propInfoCreateUtcTick != null)
            propInfoCreateUtcTick.SetValue(EntityInstance, value);
        return value;
    }

    /// <summary>
    /// Sets the UpdateUtcTick value into the data entity
    /// If you don't want to suffer the impact of reflection you can override the 
    /// SetUpdateUtcTick method (don't call the base method if you do)
    /// and set the UpdateUtcTick (or similar) field directly.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    protected virtual long SetUpdateUtcTick(long value)
    {
        if (defaultUtcHandling && EntityInstance != null && propInfoUpdateUtcTick != null)
            propInfoUpdateUtcTick.SetValue(EntityInstance, value);
        return value;
    }

    public virtual void SealEnvelope()
    {
        TypeName = CurrentTypeName;
        PK = DefaultPK;
        SK = $"{EntityInstance.Id}:";
        InternalSealEnvelope();
    }

    /// <summary>
    /// You must implement this method
    /// The EntityInstance Set method calls this method.
    /// </summary>
    protected virtual void InternalSealEnvelope()
    {
        _dbRecord = new Dictionary<string, AttributeValue>
        {
            { "TypeName", new AttributeValue() { S = TypeName } },
            { "PK", new AttributeValue() { S = PK } },
            { "SK", new AttributeValue() { S = SK } },
        };

        _dbRecord.Add("CreateUtcTick", new AttributeValue { N = CreateUtcTick.ToString() });

        _dbRecord.Add("UpdateUtcTick", new AttributeValue { N = UpdateUtcTick.ToString() });

        if (!string.IsNullOrEmpty(SK1))
            _dbRecord.Add("SK1", new AttributeValue() { S = SK1 });

        if (!string.IsNullOrEmpty(SK2))
            _dbRecord.Add("SK2", new AttributeValue() { S = SK2 });

        if (!string.IsNullOrEmpty(SK3))
            _dbRecord.Add("SK3", new AttributeValue() { S = SK3 });

        if (!string.IsNullOrEmpty(SK4))
            _dbRecord.Add("SK4", new AttributeValue() { S = SK4 });

        if (!string.IsNullOrEmpty(SK5))
            _dbRecord.Add("SK5", new AttributeValue() { S = SK5 });

        if (!string.IsNullOrEmpty(GSI1PK))
            _dbRecord.Add("GSI1PK", new AttributeValue() { S = GSI1PK });

        if (!string.IsNullOrEmpty(GSI1SK))
            _dbRecord.Add("GSI1SK", new AttributeValue() { S = GSI1SK });

        if (!string.IsNullOrEmpty(Status))
            _dbRecord.Add("Status", new AttributeValue() { S = Status });

        if (!string.IsNullOrEmpty(General))
            _dbRecord.Add("General", new AttributeValue() { S = General });

        // We always add the IsDelete attribute because we use it in filter expressions and 
        // the queries would fail with an exception if we included an attribute in a filter 
        // expression that did not exist.
        _dbRecord.Add("IsDeleted", new AttributeValue() { BOOL = IsDeleted });

        if(!string.IsNullOrEmpty(SessionId))
            _dbRecord.Add("SessionID", new AttributeValue() { S = SessionId });

        if (UseTTL)
            _dbRecord.Add("TTL", new AttributeValue { N = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds + (TTLPeriod).ToString() });
    }

    /// <summary>
    /// You must implement this method
    /// The DbRecord Set method calls this method.
    /// </summary>
    protected virtual void OpenEnvelope()
    {
        if (_dbRecord.TryGetValue("PK", out AttributeValue pk))
            PK = pk.S;

        if (_dbRecord.TryGetValue("TypeName", out AttributeValue typeName))
            TypeName = typeName.S;

        if (_dbRecord.TryGetValue("SK", out AttributeValue sk))
            SK = sk.S;

        if (_dbRecord.TryGetValue("SK1", out AttributeValue sk1))
            SK1 = sk1.S;

        if (_dbRecord.TryGetValue("SK2", out AttributeValue sk2))
            SK2 = sk2.S;

        if (_dbRecord.TryGetValue("SK3", out AttributeValue sk3))
            SK3 = sk3.S;

        if (_dbRecord.TryGetValue("SK4", out AttributeValue sk4))
            SK4 = sk4.S;

        if (_dbRecord.TryGetValue("SK5", out AttributeValue sk5))
            SK5 = sk5.S;

        if (_dbRecord.TryGetValue("GSI1PK", out AttributeValue gsi1pk))
            GSI1PK = gsi1pk.S;

        if (_dbRecord.TryGetValue("GSI1SK", out AttributeValue gsi1sk))
            GSI1SK = gsi1sk.S;

        if (_dbRecord.TryGetValue("Status", out AttributeValue status))
            Status = status.S;

        if (_dbRecord.TryGetValue("General", out AttributeValue general))
            General = general.S;

        if (_dbRecord.TryGetValue("IsDeleted", out AttributeValue isDeleted))
            IsDeleted = isDeleted.BOOL;    

        if (_dbRecord.TryGetValue("SessionId", out AttributeValue sessionId))
            SessionId = sessionId.S;

        // serialize the json data to the EntityInstance
        if (_dbRecord.TryGetValue("Data", out AttributeValue data))
        {
            JsonSize = data.S.Length;
            DeserializeData(data.S, typeName.S);
        }

        if (_dbRecord.TryGetValue("CreateUtcTick", out AttributeValue createUtcTick))
            CreateUtcTick = long.Parse(createUtcTick.N);

        if (_dbRecord.TryGetValue("UpdateUtcTick", out AttributeValue updateUtcTick))
            UpdateUtcTick = long.Parse(updateUtcTick.N);
    }

    /// <summary>
    /// Override this method if you want to customize how conversion among types
    /// This method is called from the OpenEnvelope method
    /// </summary>
    /// <param name="data"></param>
    /// <param name="typeName"></param>
    protected virtual void DeserializeData(string data, string typeName)
    {
        _entityInstance = JsonConvert.DeserializeObject<T>(data);
    }
}
