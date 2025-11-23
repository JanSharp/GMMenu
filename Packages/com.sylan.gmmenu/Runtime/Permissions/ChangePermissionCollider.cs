
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Sylan.GMMenu
{
    public enum PermissionType
    {
        DEACTIVATED = PlayerPermissions.PERMISSION_DEACTIVATED,
        PLAYER = PlayerPermissions.PERMISSION_PLAYER,
        FACILITATOR = PlayerPermissions.PERMISSION_FACILITATOR,
        GM = PlayerPermissions.PERMISSION_GM
    }
    public class ChangePermissionCollider : GMMenuPart
    {
        public PermissionType permissionType;
        public override void OnPlayerCollisionEnter()
        {
            gmMenu.PlayerPermissions.SetTempPermission(permissionType);
        }
    }
}