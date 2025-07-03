
using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace Sylan.GMMenu
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class PlayerMover : GMMenuPart
    {
        VRCPlayerApi localPlayer;
        [FieldChangeCallback(nameof(noclip))]
        bool _noclip = false;

        [NonSerialized] public bool noclipOnDoubleJump = false;
        bool jumpPressed = false;

        public float speedMagnitude = 8.0f;
        float speedLongitudinal= 0.0f;
        float speedHorizontal = 0.0f;
        float speedVertical = 0.0f;

        public Toggle usePlayerAlignedToggle;
        public Toggle usePlayerAlignedWithCorrectionToggle;
        public Toggle usePlayerAlignedWithMathToggle;
        public Toggle useRoomAlignedToggle;
        public Toggle useRoomAlignedWithCorrectionToggle;
        public Toggle useRoomAlignedWithMathToggle;


        Quaternion headVector;
        Vector3 offset;

        public Transform station;
        BoxCollider boxCollider;

        GMMenuToggle menuToggle;
        void Start()
        {
            menuToggle = gmMenu.GMMenuToggle;
            boxCollider = station.GetComponent<BoxCollider>();
            localPlayer = Networking.LocalPlayer;
        }

        public override void InputJump(bool value, UdonInputEventArgs args)
        {
            if (!noclipOnDoubleJump) return;
            if (!value) return;

            if (jumpPressed)
            {
                ToggleNoclip();
            }
            else
            {
                jumpPressed = true;
                boxCollider.enabled = false;
                SendCustomEventDelayedSeconds(nameof(ResetJumpPressed), 0.5f);
            }
        }
        public override void InputMoveVertical(float value, UdonInputEventArgs args)
        {
            speedLongitudinal = value;
        }
        public override void InputMoveHorizontal(float value, UdonInputEventArgs args)
        {
            speedHorizontal = value;
        }
        public void ResetJumpPressed()
        {
            jumpPressed = false;
            boxCollider.enabled = noclip;
        }
        public bool noclip
        {
            set
            {
                _noclip = value;
                if (value)
                {
                    station.SetParent(null,false);
                    station.position = localPlayer.GetPosition();
                    boxCollider.enabled = true;
                    localPlayer.SetGravityStrength(0);
                }
                else
                {
                    boxCollider.enabled = false;
                    localPlayer.SetGravityStrength(1);
                    localPlayer.SetVelocity(Vector3.zero);
                    station.SetParent(transform,false);
                    station.localPosition = Vector3.zero;
                    station.rotation = Quaternion.identity;
                }
            }
            get => _noclip;
        }
        void Update()
        {
            UpdateStationPosition();
        }
        public void UpdateStationPosition()
        {
            if (!noclip) return;
            //Don't teleport while staying still.
            bool isStill = (speedHorizontal == 0 && speedVertical == 0 && speedLongitudinal == 0);
            if (!isStill)
            {
                headVector = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
                offset = speedLongitudinal * (headVector * Vector3.forward);
                offset += speedHorizontal * (headVector * Vector3.right);
                offset += speedVertical * (headVector * Vector3.up);
                offset *= speedMagnitude * Time.deltaTime;
                station.position += offset;
                //localPlayer.TeleportTo(
                Teleport(
                    localPlayer,
                    station.position,
                    localPlayer.GetRotation(),
                    true
                    );
                return;
            }
            if (!menuToggle.MenuState())
            {
                //localPlayer.TeleportTo(
                Teleport(
                    localPlayer,
                    station.position,
                    localPlayer.GetRotation(),
                    true
                    );
                return;
            }
            localPlayer.SetVelocity(Vector3.zero);
            return;

        }

        private void Teleport(VRCPlayerApi player, Vector3 teleportPos, Quaternion teleportRotation, bool lerpOnRemote)
        {
            if (usePlayerAlignedToggle.isOn)
            {
                player.TeleportTo(teleportPos, teleportRotation, VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, lerpOnRemote);
            }
            else if (usePlayerAlignedWithCorrectionToggle.isOn)
            {
                player.TeleportTo(teleportPos, teleportRotation, VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, lerpOnRemote);
                player.TeleportTo(
                    teleportPos,
                    Quaternion.Inverse(localPlayer.GetRotation()) * teleportRotation * teleportRotation,
                    VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint,
                    lerpOnRemote);
            }
            else if (usePlayerAlignedWithMathToggle.isOn)
            {
                TeleportPlayerAlignedWithMath(player, teleportPos, teleportRotation, lerpOnRemote);
            }
            else if (useRoomAlignedToggle.isOn)
            {
                player.TeleportTo(teleportPos, teleportRotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, lerpOnRemote);
            }
            else if (useRoomAlignedWithCorrectionToggle.isOn)
            {
                player.TeleportTo(teleportPos, teleportRotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, lerpOnRemote);
                player.TeleportTo(
                    teleportPos,
                    Quaternion.Inverse(localPlayer.GetRotation()) * teleportRotation * teleportRotation,
                    VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint,
                    lerpOnRemote);
            }
            else if (useRoomAlignedWithMathToggle.isOn)
            {
                TeleportRoomAligned(player, teleportPos, teleportRotation, lerpOnRemote);
            }
        }

        public void TeleportCorrective(VRCPlayerApi player, Vector3 teleportPos, Quaternion teleportRotation, bool lerpOnRemote)
        {
            //This function teleports the player,
            //calculates the difference between the expected rotation and the actual rotation resulting from the teleport.
            //then teleports the player again to compensate for the error.
            //This is temporary patch for a problem where noclip causes one to spin rapidly, until I can figure out what is causing this to happen
            player.TeleportTo(
                teleportPos,
                teleportRotation,
                VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint,
                lerpOnRemote
                );
            player.TeleportTo(teleportPos,
                Quaternion.Inverse(localPlayer.GetRotation()) * teleportRotation * teleportRotation,
                VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint,
                lerpOnRemote);
        }

        public void TeleportPlayerAlignedWithMath(VRCPlayerApi player, Vector3 teleportPos, Quaternion teleportRot, bool lerpOnRemote)
        {
            //This could be simplified to have fewer intermediate variables, but separating it out like this makes it more readable for educational purposes.
            //Feel free to modify to create a more optimized version.

#if UNITY_EDITOR
            // Skip process and Exit early for ClientSim
            // since there is no play space to orient.
            player.TeleportTo(teleportPos, teleportRot);
            return;
#endif

            // teleportRot = Quaternion.Euler(0, teleportRot.eulerAngles.y, 0);

            //Get player pos/rot
            Vector3 playerPos = player.GetPosition();
            Quaternion playerRot = player.GetRotation();
            Quaternion invPlayerRot = Quaternion.Inverse(playerRot);

            //Get origin pos/rot
            VRCPlayerApi.TrackingData origin = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
            Vector3 originPos = origin.position;
            Quaternion originRot = origin.rotation;

            //Subtract player from origin in order to get the offset from the player to the origin
            //offset = origin - player
            Vector3 offsetPos = originPos - playerPos;
            Quaternion offsetRot = invPlayerRot * originRot;

            //Add the offset onto the destination in order to construct a pos/rot of where your origin would be in order to put the player at the destination
            //target = destination + offset
            Vector3 targetPos = teleportPos + teleportRot * invPlayerRot * offsetPos;
            Quaternion targetRot = teleportRot * offsetRot;

            //Apply teleportation
            player.TeleportTo(targetPos, targetRot, VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, lerpOnRemote);
        }

        public void TeleportRoomAligned(VRCPlayerApi player, Vector3 teleportPos, Quaternion teleportRot, bool lerpOnRemote)
        {
            //This could be simplified to have fewer intermediate variables, but separating it out like this makes it more readable for educational purposes.
            //Feel free to modify to create a more optimized version.

#if UNITY_EDITOR
            // Skip process and Exit early for ClientSim
            // since there is no play space to orient.
            player.TeleportTo(teleportPos, teleportRot);
            return;
#endif

            // teleportRot = Quaternion.Euler(0, teleportRot.eulerAngles.y, 0);

            //Get player pos/rot
            Vector3 playerPos = player.GetPosition();
            Quaternion playerRot = player.GetRotation();
            Quaternion invPlayerRot = Quaternion.Inverse(playerRot);

            //Get origin pos/rot
            VRCPlayerApi.TrackingData origin = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
            Vector3 originPos = origin.position;
            Quaternion originRot = origin.rotation;

            //Subtract player from origin in order to get the offset from the player to the origin
            //offset = origin - player
            Vector3 offsetPos = originPos - playerPos;
            Quaternion offsetRot = invPlayerRot * originRot;

            //Add the offset onto the destination in order to construct a pos/rot of where your origin would be in order to put the player at the destination
            //target = destination + offset
            Vector3 targetPos = teleportPos + teleportRot * invPlayerRot * offsetPos;
            Quaternion targetRot = teleportRot * offsetRot;

            //Apply teleportation
            player.TeleportTo(targetPos, targetRot, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, lerpOnRemote);
        }

        /// <summary>
        /// See: https://allenchou.net/2018/05/game-math-swing-twist-interpolation-sterp/
        /// </summary>
        public static void DecomposeSwingTwist(
            Quaternion rot,
            Vector3 twistAxis,
            out Quaternion swing,
            out Quaternion twist)
        {
            Vector3 r = new Vector3(rot.x, rot.y, rot.z);

            // singularity: rotation by 180 degree
            if (r.sqrMagnitude < Mathf.Epsilon)
            {
                Vector3 rotatedTwistAxis = rot * twistAxis;
                Vector3 swingAxis = Vector3.Cross(twistAxis, rotatedTwistAxis);

                if (swingAxis.sqrMagnitude > Mathf.Epsilon)
                {
                    float swingAngle = Vector3.Angle(twistAxis, rotatedTwistAxis);
                    swing = Quaternion.AngleAxis(swingAngle, swingAxis);
                }
                else
                {
                    // more singularity:
                    // rotation axis parallel to twist axis
                    swing = Quaternion.identity; // no swing
                }

                // always twist 180 degree on singularity
                twist = Quaternion.AngleAxis(180.0f, twistAxis);
                return;
            }

            // meat of swing-twist decomposition
            Vector3 p = Vector3.Project(r, twistAxis);
            twist = Quaternion.Normalize(new Quaternion(p.x, p.y, p.z, rot.w));
            swing = rot * Quaternion.Inverse(twist);
        }

        public void ToggleNoclip()
        {
            noclip = !noclip;
        }
        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            station.position = player.GetPosition();
        }
    }
}
