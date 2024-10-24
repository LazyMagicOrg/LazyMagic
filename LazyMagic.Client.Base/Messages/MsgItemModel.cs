using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using ReactiveUI;

namespace LazyMagic.Client.Base;

/// <summary>
/// This class is a model for a message item. It is used to create, edit, 
/// and save messages.
/// </summary>
public class MsgItemModel : MsgItem
{
    public MsgItemModel(MsgItemsModel msgItemsModel,  string filePath, MsgItem? msgItem, bool docEditable, string defaultMsg)

    {
        MsgItemsModel = msgItemsModel;
        _filePath = filePath; // Key into the MsgItesmModel dictionary
        this.WhenAnyValue(x => x.Msg)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .DistinctUntilChanged()
            .Subscribe(x => {
                MsgItemsModel.UpdatePreview();
                Dirty = !Msg.Equals(originalMsg);   
                MsgItemsModel.MessageSet.Messages.Refresh();
            });
        if (msgItem != null)
        {
            originalMsg = Msg = msgItem.Msg;
            Editable = msgItem.Editable ?? false || docEditable;
        } else
        {
            if(docEditable)
            {
                originalMsg = Msg = defaultMsg;
                Editable = true;
            }
        }
    }

    #region Public Properties
    public MsgItemsModel? MsgItemsModel { get; private set; }
    public DocMetaData DocMetaData => MsgItemsModel!.MessageSet.MessageDocs[_filePath].DocMetaData;
    private bool _isDirty = false;
    public bool Dirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }
    #endregion

    #region private fields
    private string _filePath  = string.Empty;
    private string? originalMsg = "";
    #endregion

    #region public methods 
    public void CancelEdit()
    {
        Msg = originalMsg ?? "";
        Dirty = false;
    }

    #endregion
}
