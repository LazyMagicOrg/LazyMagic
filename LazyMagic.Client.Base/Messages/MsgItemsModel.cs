using System;
using System.Collections.Generic;
using System.Text;
using System.Linq.Expressions;
using System.Linq;
using ReactiveUI;

namespace LazyMagic.Client.Base
{
    public class MsgItemsModel : NotifyBase
    {

        public MsgItemsModel(LzMessageSet messageSet, string key) 
        { 
            MessageSet = messageSet;
            this.Key = key;
            UpdateItems();
        }  

        #region Public Propeties
        public string Key { get; private set; } 
       
        public Dictionary<string, MsgItemModel> Items { get; } = new Dictionary<string, MsgItemModel>(); // key is Doc FilePath
        private string _imperialPreview = "";
        public string ImperialPreview 
        {   get => _imperialPreview; 
            private set => SetProperty(ref _imperialPreview, value); 
        }
        public string _metricPreview = "";
        public string MetricPreview 
        {   get => _metricPreview; 
            private set=> SetProperty(ref _metricPreview, value); } 
        public List<MsgItemModel> ItemsOrderedByPrecedents { get; } = new List<MsgItemModel>();
        public LzMessageSet MessageSet { get; private set; }
        public bool Dirty
        {
            get
            {
                foreach (var item in Items.Values)
                    if (item.Dirty)
                        return true;

                return false;
            }
        }
        #endregion

        #region Private Members
        #endregion

        #region Private Methods

        private Dictionary<string, MsgItemModel> UpdateItems()
        {

            var lastMsg = ""; // Used to provide default on new MstgItem creation
            foreach (var messageDoc in MessageSet.MessageDocs)
            {
                if (Items.TryGetValue( messageDoc.Key, out MsgItemModel? existingMsgItemModel))
                {
                    if (existingMsgItemModel.Dirty)
                    {
                        Items.Add(messageDoc.Key, existingMsgItemModel);
                        continue;
                    }
                }
                MsgItem? msgItem;
                _ = messageDoc.Value.Messages.TryGetValue(Key, out msgItem);
                var isEditable = (msgItem != null && (msgItem.Editable ?? false)) || messageDoc.Value.DocMetaData.Editable;
                var isEmpty = msgItem == null || string.IsNullOrEmpty(msgItem.Msg);

                if (!isEditable && isEmpty)
                    continue;

                var msgItemModel = new MsgItemModel(this, messageDoc.Key, msgItem, messageDoc.Value.DocMetaData.Editable, lastMsg);

                //if (msgItem is not null)
                //{
                //    if (!msgItemModel.Dirty)
                //    {
                //        msgItemModel.Msg = msgItem.Msg;
                //        msgItemModel.Editable = msgItem.Editable ?? messageDoc.Value.DocMetaData.Editable;
                //    }
                //}
                //else
                //{
                //    msgItemModel.Msg = lastMsg;
                //    msgItemModel.Editable = messageDoc.Value.DocMetaData.Editable;
                //}
                lastMsg = msgItemModel.Msg;

                Items.Add(messageDoc.Key, msgItemModel);
            }

            List<string> orderedDocListKeys =
                MessageSet
                .MessageDocs
                .Reverse()
                .Select(x => x.Key).ToList();

            ItemsOrderedByPrecedents.Clear();
            foreach (var docKey in orderedDocListKeys)
                if(Items.ContainsKey(docKey))
                    ItemsOrderedByPrecedents.Add(Items[docKey]);
            UpdatePreview();
            RaisePropertyChanged(nameof(ItemsOrderedByPrecedents));
            return Items;
        }
        public void UpdatePreview()
        {
            MessageSet.UpdateMsgs(unitsArg: null, key: Key);
            MetricPreview = MessageSet!.Msg(Key, LzMessageUnits.Metric);
            ImperialPreview = MessageSet!.Msg(Key, LzMessageUnits.Imperial);
        }
        #endregion
    }
}
