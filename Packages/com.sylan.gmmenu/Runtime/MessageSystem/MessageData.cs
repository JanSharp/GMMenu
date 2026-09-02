
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace Sylan.GMMenu
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class MessageData : GMMenuPart
    {
        const int OWNER_NULL = -1;
        const float TIME_NULL = -1.0f;
        const float TIME_UNTIL_STALE = 5 * 60.0f;

        public const int MESSAGE_NULL = -1;
        public const int MESSAGE_EMPTY = 0;
        public const int MESSAGE_URGENT = 1;
        public const int MESSAGE_ROLL = 2;
        public const int MESSAGE_QUESTION = 3;
        public const int MESSAGE_SILENT = 4;
        public const int MESSAGE_GMRADIO = 5;

        MessageSyncManager messageSyncManager;
        [UdonSynced]
        int _ownerID = OWNER_NULL;

        [FieldChangeCallback(nameof(owner))]
        VRCPlayerApi _owner;

        [SerializeField, UdonSynced, FieldChangeCallback(nameof(message))]
        int _message = MESSAGE_NULL;

        public float timeReceived = TIME_NULL;
        public float timeRead = TIME_NULL;

        public bool isReadLocal = true;
        public bool isReadRemote = true;

        void Start()
        {
            messageSyncManager = gmMenu.MessageSyncManager;
        }
        public VRCPlayerApi owner
        {
            set
            {
                Debug.Log($"[GMMenu] Assigning {name} to {GetPlayerName(value)}");
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                _owner = value;
                if (!Utilities.IsValid(value))
                {
                    _ownerID = OWNER_NULL;
                    message = MESSAGE_NULL;
                    timeReceived = TIME_NULL;

                    RequestSerializationAndLog();
                    return;
                }
                var id = VRCPlayerApi.GetPlayerId(value);
                if (id != _ownerID) message = MESSAGE_NULL;
                _ownerID = id;
                RequestSerializationAndLog();
            }
            get => _owner;
        }
        public int message
        {
            set
            {
                Debug.Log($"[GMMenu] Changing message from {MessageToString(_message)} "
                    + $"to {MessageToString(value)} for {GetScriptDisplayName()}");
                _message = value;
                RequestSerializationAndLog();
                isReadLocal = false;
                isReadRemote = false;
                timeReceived = TIME_NULL;
                if (value == MESSAGE_NULL)
                {
                    timeRead = TIME_NULL;
                    messageSyncManager.SendMessageUpdateEvent();
                    return;
                }
                timeReceived = Time.time;
                SendOnNewMessageEvent();
            }
            get => _message;
        }
        public string PrintMessage()
        {
            switch (message)
            {
                case MESSAGE_EMPTY:
                    return "";
                case MESSAGE_URGENT:
                    return owner.displayName + " requires urgent assistance.";
                case MESSAGE_ROLL:
                    return owner.displayName + " needs a roll.";
                case MESSAGE_QUESTION:
                    return owner.displayName + " has a question.";
                case MESSAGE_SILENT:
                    return "Join " + owner.displayName + " silently.";
                case MESSAGE_GMRADIO:
                    return owner.displayName + " requests GM Radio.";

                default:
                    return "Invalid Messge.";
            }
        }
        public bool IsRead()
        {
            return isReadLocal || isReadRemote;
        }
        public void SyncReadStatus(bool isRead)
        {
            Debug.Log($"[GMMenu] {nameof(SyncReadStatus)} on {GetScriptDisplayName()}, isRead: {isRead}");
            isReadLocal = isRead;
            if (isReadRemote)
            {
                messageSyncManager.SendMessageUpdateEvent();
                return;
            }
            if (isRead)
            {
                SendOnReadRemoteEvent();
                SetReadTime();
                messageSyncManager.SendMessageUpdateEvent();
                SendOnMessageStaleEvent();
                return;
            }
            SendOnUndoReadRemoteEvent();
            ResetReadTime();
        }
        void SetReadTime()
        {
            if (timeRead == TIME_NULL) timeRead = Time.time;
        }
        void ResetReadTime()
        {
            timeRead = TIME_NULL;
        }
        //Events
        public override void OnPreSerialization()
        {
            Debug.Log($"[GMMenu] {nameof(OnPreSerialization)} on {GetScriptDisplayName()} with message {MessageToString(message)}");
        }
        public override void OnDeserialization()
        {
            Debug.Log($"[GMMenu] {nameof(OnDeserialization)} on {GetScriptDisplayName()}, "
                + $"prev assigned to {GetPlayerName(_owner)}, "
                + $"now assigned to {GetPlayerName(_ownerID == OWNER_NULL ? null : VRCPlayerApi.GetPlayerById(_ownerID))}, "
                + $"message: {MessageToString(message)}");
            //Set _owner from synced _ownerID
            if (_ownerID == OWNER_NULL)
            {
                _owner = null;
                return;
            }
            _owner = VRCPlayerApi.GetPlayerById(_ownerID);
        }
        public void SendOnNewMessageEvent()
        {
            Debug.Log($"[GMMenu] Raising event due to new message from {GetScriptDisplayName()}");
            messageSyncManager.OnNewMessage(this);
        }
        public void SendOnReadRemoteEvent()
        {
            Debug.Log($"[GMMenu] {nameof(SendOnReadRemoteEvent)} on {GetScriptDisplayName()}");
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(OnReadRemote));
        }
        public void OnReadRemote()
        {
            Debug.Log($"[GMMenu] {nameof(OnReadRemote)} on {GetScriptDisplayName()}");
            if (!isReadLocal) isReadRemote = true;
            messageSyncManager.SendMessageUpdateEvent();
            SetReadTime();
            SendOnMessageStaleEvent();
        }
        public void SendOnUndoReadRemoteEvent()
        {
            Debug.Log($"[GMMenu] {nameof(SendOnUndoReadRemoteEvent)} on {GetScriptDisplayName()}");
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(OnUndoReadRemote));
        }
        public void OnUndoReadRemote()
        {
            Debug.Log($"[GMMenu] {nameof(OnUndoReadRemote)} on {GetScriptDisplayName()}");
            isReadRemote = false;
            if (isReadLocal) SendOnReadRemoteEvent();
            messageSyncManager.SendMessageUpdateEvent();
        }
        public void SendOnMessageStaleEvent()
        {
            //Add one to avoid floating point precision messing with comparison
            SendCustomEventDelayedSeconds(nameof(OnMessageStale), TIME_UNTIL_STALE + 1.0f);
        }
        public void OnMessageStale()
        {
            if (message == MESSAGE_NULL) return;
            if (!IsRead()) return;
            var timePassed = Time.time - timeRead;
            if (timePassed <= TIME_UNTIL_STALE)
            {
                SendCustomEventDelayedSeconds(nameof(OnMessageStale), TIME_UNTIL_STALE - timePassed + 1.0f);
                return;
            }
            Debug.Log($"[GMMenu] Message turned stale for {GetScriptDisplayName()}");
            message = MESSAGE_NULL;
            messageSyncManager.SendMessageUpdateEvent();
        }

        private void RequestSerializationAndLog()
        {
            VRCPlayerApi networkingOwner = Networking.GetOwner(gameObject);
            Debug.Log($"[GMMenu] Requesting to sync {GetScriptDisplayName()}, message: {MessageToString(message)}, "
                + $"networking owner: {GetPlayerName(networkingOwner)} "
                + $"- isLocal: {(Utilities.IsValid(networkingOwner) ? networkingOwner.isLocal.ToString() : "<null>")}");
            RequestSerialization();
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            Debug.Log($"[GMMenu] Networking ownership of {GetScriptDisplayName()} changed to {GetPlayerName(Networking.GetOwner(gameObject))}");
        }

        public static string MessageToString(int message)
        {
            switch (message)
            {
                case MESSAGE_NULL:
                    return $"({message} | null)";
                case MESSAGE_EMPTY:
                    return $"({message} | empty)";
                case MESSAGE_URGENT:
                    return $"({message} | urgent)";
                case MESSAGE_ROLL:
                    return $"({message} | roll)";
                case MESSAGE_QUESTION:
                    return $"({message} | question)";
                case MESSAGE_SILENT:
                    return $"({message} | silent)";
                case MESSAGE_GMRADIO:
                    return $"({message} | gm radio)";
                default:
                    return $"({message} | undefined)";
            }
        }

        public string GetScriptDisplayName()
        {
            return $"[{name} assigned to {GetPlayerName(owner)}]";
        }

        public static string GetPlayerName(VRCPlayerApi player)
        {
            return Utilities.IsValid(player) ? player.displayName : "<null>";
        }
    }
}
