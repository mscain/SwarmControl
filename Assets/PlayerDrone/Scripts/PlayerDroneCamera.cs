using UnityEngine;
using ExtensionMethods;

public class PlayerDroneCamera : MonoBehaviour {
    #region Variables

    public GameObject drone, cannon;
    public PlayerDrone droneS;
    public float sensitivity = 1f;
    public float lookSpeed, lookFactor;
    public float turnUpBound = 90;

    public float aimSensitivity = 1;
    public float smoothY, smoothX;
    public float turnedY, turnedY2, turnedX, turnedX2;
    public bool X, Y;

    #endregion

    private void Update() {
        if(droneS.isControlled)
            smoothX = aimSensitivity * sensitivity * Input.GetAxis("Mouse X") * 15f;
        if(X) {
            if(droneS.glidey) {
                droneS.turnInput = smoothX / 2;
            } else {
                turnedX2 += smoothX;
                turnedX2 = turnedX2 % 360;
                turnedX = Extensions.SharpInDampAngle(turnedX, turnedX2, lookSpeed * 3, lookFactor) % 360;
            }
        }

        if(droneS.isControlled)
            smoothY = -aimSensitivity * sensitivity * Input.GetAxis("Mouse Y") * 15f;
        if(Y)
            turnedY2 += smoothY;
        turnedY2 = Mathf.Clamp(turnedY2, -turnUpBound, turnUpBound) % (turnUpBound + 1);
        turnedY = Extensions.SharpInDampAngle(turnedY, turnedY2, lookSpeed * 4, lookFactor) % (turnUpBound + 1);
        transform.localEulerAngles = new Vector3(0, turnedY - 180, 90);
        cannon.transform.localEulerAngles = new Vector3(0, turnedY - 180 - droneS.cannonAngle, 90);
        if(!droneS.glidey) {
            drone.transform.localEulerAngles = new Vector3(drone.transform.localRotation.x, 180 + turnedX, drone.transform.localRotation.z);
        }
    }
}