using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraScript : MonoBehaviour {
    /*
	Controls:
		WASD/Arrows: Movement
		          Q: Climb
		          E: Drop
              Shift: Move faster
            Control: Move slower
                End: Toggle cursor locking to screen (you can also press Ctrl+P to toggle play mode on and off).
	*/

    #region Variables

	public float cameraSensitivity = 90;
	public float climbSpeed = 4;
	public float normalMoveSpeed = 10;
	public float slowMoveFactor = 0.25f;
	public float fastMoveFactor = 3;
	public bool startCursorLocked;

	float rotationX = 0.0f;
	float rotationY = 0.0f;

	#endregion

    void Start()
    {
		Cursor.lockState = startCursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
    }

    void Update()
    {
        rotationX += Input.GetAxis("Mouse X") * cameraSensitivity * Time.deltaTime;
        rotationY += Input.GetAxis("Mouse Y") * cameraSensitivity * Time.deltaTime;
        rotationY = Mathf.Clamp(rotationY, -90, 90);

        transform.localRotation = Quaternion.AngleAxis(rotationX, Vector3.up);
        transform.localRotation *= Quaternion.AngleAxis(rotationY, Vector3.left);

        transform.position += transform.forward * normalMoveSpeed * Input.GetAxis("Vertical") * Time.deltaTime *
			(Input.GetAxis("Shift") > 0.1f ? fastMoveFactor : 1) * (Input.GetAxis("Ctrl") > 0.1f ? slowMoveFactor : 1);
		transform.position += transform.right * normalMoveSpeed * Input.GetAxis("Horizontal") * Time.deltaTime *
			(Input.GetAxis("Shift") > 0.1f ? fastMoveFactor : 1) * (Input.GetAxis("Ctrl") > 0.1f ? slowMoveFactor : 1);


        if (Input.GetKey(KeyCode.Q)) { transform.position += transform.up * climbSpeed * Time.deltaTime; }
        if (Input.GetKey(KeyCode.E)) { transform.position -= transform.up * climbSpeed * Time.deltaTime; }

        if (Input.GetKeyDown(KeyCode.End))
        {
			Cursor.lockState = (Cursor.lockState == CursorLockMode.Locked) ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }
}