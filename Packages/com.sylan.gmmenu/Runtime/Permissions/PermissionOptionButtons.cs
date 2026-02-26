
using JetBrains.Annotations;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace Sylan.GMMenu
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PermissionOptionButtons : GMMenuPart
    {
        [NotNull,SerializeField] Image gmButton, facilitatorButton, playerButton;
        Color activeColor = new Color(207f / 255f, 44f / 255f, 179f / 255f, 191f / 255f);
        Color inactiveColor = new Color(153f / 255f, 76f / 255f, 140f / 255f, 191f / 255f);

        void Start()
        {
            gmMenu.PlayerPermissions.AddListener(this);
            OnPermissionUpdate();
        }
        public void SetPermissionGM()
        {
            gmMenu.PlayerPermissions.SetTempPermission(PlayerPermissions.PERMISSION_GM);
        }
        public void SetPermissionFacilitator()
        {
            gmMenu.PlayerPermissions.SetTempPermission(PlayerPermissions.PERMISSION_FACILITATOR);
        }
        public void SetPermissionPlayer()
        {
            gmMenu.PlayerPermissions.SetTempPermission(PlayerPermissions.PERMISSION_PLAYER);
        }
        public void OnPermissionUpdate()
        {
            var permissionLevel = gmMenu.PlayerPermissions.getPermissionLevel();
            gmButton.color = inactiveColor;
            facilitatorButton.color = inactiveColor;
            playerButton.color = inactiveColor;
            switch (permissionLevel)
            {
                case PlayerPermissions.PERMISSION_GM:
                    gmButton.color = activeColor;
                    break;
                case PlayerPermissions.PERMISSION_FACILITATOR:
                    facilitatorButton.color = activeColor;
                    break;
                case PlayerPermissions.PERMISSION_PLAYER:
                    playerButton.color = activeColor;
                    break;
            }
        }
    }
}
