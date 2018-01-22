using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ExtensionMethods;

public class SwarmDrone : MonoBehaviour {
    #region Variables

    public bool isControlledBySwarm = true;
    public float updateFrequency = 10; //e.g. 10 times / second
    public float fwdControl, turnControl;
    public Vector3 moveVector; //used if isControlledBySwarm
    public List<WheelCollider> wheelsR, wheelsL;
    public MeshRenderer headTexture;
    public float speed = 50f;
    public float turnSpeed = 5f;
    public float turnSharpness = 0.3f; //defines how sharp turns should be when changing moveVector
    private Rigidbody _rb;

    #endregion

    private void Start() {
        _rb = GetComponent<Rigidbody>();
        headTexture.material.color = Color.HSVToRGB(Random.Range(0, 360) / 360f, Random.Range(20, 100) / 100f,
            Random.Range(50, 100) / 100f);

        StartCoroutine(FlockingControl());
    }

    private void FixedUpdate() {
        if(isControlledBySwarm)
            SetMoveByVector();
        Move(fwdControl, turnControl);
    }

    /// <summary>
    ///     Sets bot movement based on flocking algorithm.
    /// </summary>
    private IEnumerator FlockingControl() {
        while(true) {
            Vector3 flockingControlVector = CalcProximalControl();


            flockingControlVector = Vector3.ClampMagnitude(flockingControlVector, 1);
            if(isControlledBySwarm)
                moveVector = flockingControlVector;

            yield return new WaitForSeconds(1 / updateFrequency); //Run updateFrequency times a second
        }
    }

    private Vector3 CalcProximalControl() {
        Vector3 pControlVec = Vector3.zero;
        return pControlVec;
    }

    /// <summary>
    ///     Sets bot movement based on a vector, where vector magnitude controls bot speed.
    /// </summary>
    /// <param name="moveVector">Vector that points where the bot should go, with magnitude between 0 and 1</param>
    private void SetMoveByVector() {
        Debug.DrawRay(transform.position, moveVector / 2f, Color.green);

        float fwdSpeedControl = Vector3.Dot(transform.forward, moveVector.normalized); //Slows us down for turns
        fwdSpeedControl = Mathf.Lerp(moveVector.magnitude, moveVector.magnitude * fwdSpeedControl, turnSharpness);
        fwdControl = Extensions.SharpInDamp(fwdControl, fwdSpeedControl, 1f);

        //Turns faster the larger the angle is, slows down as we approach desired heading
        var turnSpeedControl = Mathf.Deg2Rad * Vector3.Angle(transform.forward, moveVector);
        Debug.Log(turnSpeedControl);
        turnSpeedControl *= Mathf.Lerp(1f, .5f, Mathf.Lerp(0, fwdControl, turnSpeedControl / 3));

        //Decides which direction we should be turning
        var turnDirectionControl = Vector3.Dot(transform.right, moveVector.normalized);
        turnDirectionControl = turnDirectionControl / Mathf.Abs(turnDirectionControl + 0.01f);

        turnControl = turnDirectionControl * turnSpeedControl;
    }

    /// <summary>
    ///     Controls the bot movement. Should only be called once per physics timestep (last call will always override).
    /// </summary>
    /// <param name="fwdInput">Float between -1 and 1 that controls bots forward movement</param>
    /// <param name="turnInput">Float between -1 and 1 specifying how fast to turn. 1 is full right, -1 full left.</param>
    private void Move(float fwdInput, float turnInput) {
        foreach(var wheel in wheelsR)
            wheel.motorTorque = fwdInput * speed - turnInput * turnSpeed;
        foreach(var wheel in wheelsL)
            wheel.motorTorque = fwdInput * speed + turnInput * turnSpeed;
    }
}