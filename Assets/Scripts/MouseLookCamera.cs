/*Try making it so that only the upper body turns up to a certain point, then the legs turn with. When walking, turn the whole body and the upper body in opposite directions,
So that essentially only the legs are turning, and do this till the legs are in the same direction.


Add Comments explaining purpose of blocks of code and unclear variables and such */

using UnityEngine;
using ExtensionMethods;
public class MouseLookCamera : MonoBehaviour {
	#region Variables
	public GameObject holder, drone, cannon, barrel;
	public MouseLook droneS;
	public float sensitivity = 1f;
	public float lookSpeed, lookFactor;
	public float turnUpBound = 90;
	public float turnRightBound = 75;

	public float currentTargetCameraAngle = 60;
	public float aimSensitivity = 1;
	public float smoothY, smoothX, smoothForceTurn;
	float manV, headV, ratioZoomV;
	public float turnedY, turnedY2, turnedX, turnedX2, turnedXSpine, turnedXSpine2, turnedXSpineLate;
	public bool X, Y;
	#endregion
	void Awake(){
	}
	void Update(){
		if(droneS.isControlled)
			smoothX = aimSensitivity * sensitivity * Input.GetAxis("Mouse X") * 15f;
		if(X){
			if(droneS.glidey){
				droneS.turnInput = smoothX/2;
			} else{
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
		if(!droneS.glidey){
			drone.transform.localEulerAngles = new Vector3(drone.transform.localRotation.x, 180 + turnedX, drone.transform.localRotation.z);
		}

	}
	void LateUpdate(){
		turnedXSpineLate = turnedXSpine;
	}
}