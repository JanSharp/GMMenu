using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace Sylan.GMMenu
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class MessageSyncManager : GMMenuPart
    {
        private MessageData[] messageData;
        private MessageData[] sortedMessages;
        private MessageData localMessage = null;
        private const float EnsureHasLocalMessageLoopInterval = 2f;
        private UdonSharpBehaviour[] NewMessageEventListeners = new UdonSharpBehaviour[0];

        private void Start()
        {
            messageData = GetComponentsInChildren<MessageData>();
        }
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            Debug.Log($"[GMMenu] Networking ownership of MessageSyncManager changed to {MessageData.GetPlayerName(Networking.GetOwner(gameObject))}");
        }
        //Manage Message Data Ownership
        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            Debug.Log($"[GMMenu] Joined player: {MessageData.GetPlayerName(player)}, "
                + $"MessageSyncManager owner: {MessageData.GetPlayerName(Networking.GetOwner(gameObject))}");
            if (Networking.IsOwner(gameObject))
            {
                SetMessageOwnership(player);
            }
            else if (player.isLocal)
            {
                SendCustomEventDelayedSeconds(nameof(EnsureLocalMessageDataGotAssignedLoop), EnsureHasLocalMessageLoopInterval);
            }
        }
        private void SetMessageOwnership(VRCPlayerApi player)
        {
            foreach (MessageData m in messageData)
            {
                if (m.owner != null) continue;

                m.owner = player;
                return;
            }
            Debug.LogError($"[GMMenu] Unable to assign MessageData script to {MessageData.GetPlayerName(player)}");
        }
        public void EnsureLocalMessageDataGotAssignedLoop()
        {
            if (GetMessageByOwner(Networking.LocalPlayer) != null) return;
            Debug.Log($"[GMMenu] No {nameof(MessageData)} has been assigned to us, request again. "
                + $"MessageSyncManager owner: {MessageData.GetPlayerName(Networking.GetOwner(gameObject))}");
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RequestReconfirmationOfMessageDataAssignment), Networking.LocalPlayer.playerId);
            SendCustomEventDelayedSeconds(nameof(EnsureLocalMessageDataGotAssignedLoop), EnsureHasLocalMessageLoopInterval);
        }
        [NetworkCallable(maxEventsPerSecond: 20)]
        public void RequestReconfirmationOfMessageDataAssignment(int playerId)
        {
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerId);
            if (!Utilities.IsValid(player)) return;
            MessageData message = GetMessageByOwner(player);
            // VRChat is supposed to guarantee that every player is going to agree what the
            // latest synced state of a script is.
            // Turns out when requesting serialization at exactly around the time when a player joins,
            // the synced data will be sent to all players, except sometimes not to the joining player.
            if (message != null)
            {
                // Re-request serialization to work around the issue.
                Debug.LogError($"[GMMenu] Reconfirming assignment of {message.GetScriptDisplayName()}");
                message.owner = player;
            }
            else
            {
                // Oh, they actually have no script assigned to them,
                // might happen when the master left at an inopportune time.
                Debug.LogError($"[GMMenu] Master must have left at an inopportune time, "
                    + $"{MessageData.GetPlayerName(player)} is asking for reconfirmation "
                    + $"but no {nameof(MessageData)} is even assigned to them.");
                SetMessageOwnership(player);
            }
        }
        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            Debug.Log($"[GMMenu] Left player: {MessageData.GetPlayerName(player)}, "
                + $"MessageSyncManager owner: {MessageData.GetPlayerName(Networking.GetOwner(gameObject))}");
            if (!Networking.IsOwner(gameObject)) return;
            RevokeMessageOwnership(player);
        }
        private void RevokeMessageOwnership(VRCPlayerApi player)
        {
            int unassignedCount = 0;
            foreach (MessageData m in messageData)
            {
                if (m.owner != player) continue;

                m.owner = null;
                unassignedCount++;
            }
            if (unassignedCount == 0)
                Debug.LogError($"[GMMenu] No MessageData script was even assigned to {MessageData.GetPlayerName(player)}");
            else if (unassignedCount > 1)
                Debug.LogError($"[GMMenu] More than 1 ({unassignedCount}) MessageData scripts were assigned to {MessageData.GetPlayerName(player)}");
        }
        //Get MessageData that belongs to a specific player, or a list of players
        public MessageData GetMessageByOwner(VRCPlayerApi player)
        {
            foreach (MessageData m in messageData)
            {
                if (m.owner == player) return m;
            }
            return null;
        }
        public MessageData GetLocalMessage()
        {
            return localMessage;
        }
        public int GetLocalMessageValue()
        {
            if (!Utilities.IsValid(localMessage)) return MessageData.MESSAGE_NULL;
            return localMessage.message;
        }
        int CompareMessageTime(MessageData message1, MessageData message2)
        {
            if (message1.timeReceived == message2.timeReceived) return 0;
            if (message1.timeReceived < message2.timeReceived) return -1;
            return 1;
        }
        public MessageData[] GetMessages()
        {
            return sortedMessages;
        }

        public MessageData[] SortMessages()
        {
            //Terrible code to sort messages, because UDON doesn't support built in C# stuff
            var unreadEmergencies = new MessageData[0];
            var readEmergencies = new MessageData[0];

            var unreadNonEmergencies = new MessageData[0];
            var readNonEmergencies = new MessageData[0];

            foreach (MessageData message in messageData)
            {
                if (!Utilities.IsValid(message)) continue;
                if (message.message == MessageData.MESSAGE_NULL) continue;

                if (message.message == MessageData.MESSAGE_URGENT)
                {
                    if (message.IsRead())
                    {
                        Utils.ArrayUtils.Append(ref readEmergencies, message);
                        continue;
                    }
                    Utils.ArrayUtils.Append(ref unreadEmergencies, message);
                    continue;
                }

                if (message.IsRead())
                {
                    Utils.ArrayUtils.Append(ref readNonEmergencies, message);
                    continue;
                }
                Utils.ArrayUtils.Append(ref unreadNonEmergencies, message);
            }

            QuickSortMessages(unreadEmergencies, 0, unreadEmergencies.Length - 1);
            QuickSortMessages(readEmergencies, 0, readEmergencies.Length - 1);
            QuickSortMessages(unreadNonEmergencies, 0, unreadNonEmergencies.Length - 1);
            QuickSortMessages(readNonEmergencies, 0, readNonEmergencies.Length - 1);

            var sortedMessages = new MessageData
                [unreadEmergencies.Length + readEmergencies.Length +
                unreadNonEmergencies.Length + readNonEmergencies.Length];
            var i = 0;

            unreadEmergencies.CopyTo(sortedMessages, i);
            i += unreadEmergencies.Length;
            readEmergencies.CopyTo(sortedMessages, i);
            i += readEmergencies.Length;
            unreadNonEmergencies.CopyTo(sortedMessages, i);
            i += unreadNonEmergencies.Length;
            readNonEmergencies.CopyTo(sortedMessages, i);

            return sortedMessages;
        }

        //Set or Get contents of a message
        public void SetMessage(VRCPlayerApi player, int message)
        {
            var m = GetMessageByOwner(player);
            if (!Utilities.IsValid(m))
            {
                Debug.LogError($"[GMMenu] Attempt to {nameof(SetMessage)} for {MessageData.GetPlayerName(player)} to "
                    + $"{MessageData.MessageToString(message)}, however no {nameof(MessageData)} is associated with this player.");
                return;
            }
            Networking.SetOwner(Networking.LocalPlayer, m.gameObject);
            if (player.isLocal) localMessage = m;
            m.message = message;
        }
        //Events
        public void OnNewMessage(MessageData m)
        {
            SendNewMessageEvent();
        }
        public void SendNewMessageEvent()
        {
            sortedMessages = SortMessages();
            Utils.Events.SendEvent("OnNewMessage", NewMessageEventListeners);
        }
        public void SendMessageUpdateEvent()
        {
            sortedMessages = SortMessages();
            Utils.Events.SendEvent("OnMessageUpdate", NewMessageEventListeners);
        }
        public void AddListener(UdonSharpBehaviour b)
        {
            Utils.ArrayUtils.Append(ref NewMessageEventListeners, b);
        }
        //Quicksort
        [RecursiveMethod]
        private void QuickSortMessages(MessageData[] arr, int start, int end)
        {
            if (!Utilities.IsValid(arr)) return;

            int i = 0;
            if (start < end)
            {
                i = PartitionMessages(arr, start, end);

                QuickSortMessages(arr, start, i - 1);
                QuickSortMessages(arr, i + 1, end);
            }
        }
        private int PartitionMessages(MessageData[] arr, int start, int end)
        {
            MessageData temp;
            MessageData p = arr[end];
            int i = start - 1;

            for (int j = start; j <= end - 1; j++)
            {
                if (CompareMessageTime(arr[j], p) == -1)
                {
                    i++;
                    temp = arr[i];
                    arr[i] = arr[j];
                    arr[j] = temp;
                }
            }

            temp = arr[i + 1];
            arr[i + 1] = arr[end];
            arr[end] = temp;
            return i + 1;
        }

    }
}
