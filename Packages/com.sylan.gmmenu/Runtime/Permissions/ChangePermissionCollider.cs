using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Sylan.GMMenu
{
    public enum PermissionType
    {
        Deactivated = PlayerPermissions.PERMISSION_DEACTIVATED,
        Player = PlayerPermissions.PERMISSION_PLAYER,
        Facilitator = PlayerPermissions.PERMISSION_FACILITATOR,
        Gm = PlayerPermissions.PERMISSION_GM
    }
    public class ChangePermissionCollider : GMMenuPart
    {
        public PermissionType permissionType;
        public override void OnPlayerCollisionEnter(VRCPlayerApi player)
        {
            if (!player.isLocal) return;
            gmMenu.PlayerPermissions.SetTempPermission((int)permissionType);
        }
    }
}