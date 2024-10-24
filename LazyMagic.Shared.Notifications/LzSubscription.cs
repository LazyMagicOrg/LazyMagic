namespace LazyMagic.Shared
{
    using System = global::System;

 
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "13.19.0.0 (NJsonSchema v10.9.0.0 (Newtonsoft.Json v13.0.3.0))")]
    public partial class LzSubscription : System.ComponentModel.INotifyPropertyChanged
    {
        private string _id;
        private string _connectionId;
        private string _userId;
        private System.Collections.Generic.ICollection<string> _topicIds;
        private long _createUtcTick;
        private long _updateUtcTick;

        /// <summary>
        /// subscriptionId
        /// </summary>
        [Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Id
        {
            get { return _id; }

            set
            {
                if (_id != value)
                {
                    _id = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// web socket connectionId
        /// </summary>
        [Newtonsoft.Json.JsonProperty("connectionId", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string ConnectionId
        {
            get { return _connectionId; }

            set
            {
                if (_connectionId != value)
                {
                    _connectionId = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>
        /// lzUserId
        /// </summary>
        [Newtonsoft.Json.JsonProperty("userId", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string UserId
        {
            get { return _userId; }

            set
            {
                if (_userId != value)
                {
                    _userId = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("topicIds", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public System.Collections.Generic.ICollection<string> TopicIds
        {
            get { return _topicIds; }

            set
            {
                if (_topicIds != value)
                {
                    _topicIds = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("createUtcTick", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public long CreateUtcTick
        {
            get { return _createUtcTick; }

            set
            {
                if (_createUtcTick != value)
                {
                    _createUtcTick = value;
                    RaisePropertyChanged();
                }
            }
        }

        [Newtonsoft.Json.JsonProperty("updateUtcTick", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public long UpdateUtcTick
        {
            get { return _updateUtcTick; }

            set
            {
                if (_updateUtcTick != value)
                {
                    _updateUtcTick = value;
                    RaisePropertyChanged();
                }
            }
        }

        private System.Collections.Generic.IDictionary<string, object> _additionalProperties;

        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new System.Collections.Generic.Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }

}
