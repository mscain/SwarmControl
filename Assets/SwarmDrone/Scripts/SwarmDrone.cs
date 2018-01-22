using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ExtensionMethods;
using Random = UnityEngine.Random;

public class SwarmDrone : MonoBehaviour {
    #region Variables

    public bool isControlledBySwarm = true;
    public float updateFrequency = 10; //e.g. 10 times / second
    public float proxMult, alignMult, goalMult;
    public float scanRadius = 5f; //How far each bot scans for other bots
    public float swarmSpread = 1f; //How far each robot should stay from other robots
    public float repulsionForce = 0.02f; //How strongly they should repel away
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
        headTexture.material.color = Color.HSVToRGB(Random.Range(0f, 1f), Random.Range(.2f, 1f), Random.Range(.5f, 1f));

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
            Vector3 flockingControlVector = proxMult * CalcProximalControl()
                                          + alignMult * CalcAlignControl()
                                          + goalMult * CalcGoalControl();


            if(gameObject.name.Equals("SwarmDrone")) {
                Debug.Log(flockingControlVector);
            }

            flockingControlVector = Vector3.ClampMagnitude(flockingControlVector, 1);
            if(isControlledBySwarm)
                moveVector = flockingControlVector;

            yield return new WaitForSeconds(1 / updateFrequency); //Run updateFrequency times a second
        }
    }

    private Vector3 CalcProximalControl() {
        Vector3 proxVec = Vector3.zero;

        //select headColliders within scanRadius of this bot
        foreach(Collider headCollider in Physics.OverlapSphere(transform.position, scanRadius, 1 << 11)) {
            Transform botT = headCollider.transform.parent;
            if(botT.gameObject.Equals(gameObject))
                continue; //Skip this bot

            float di = Vector3.Distance(transform.position, botT.position);
            float sigma = swarmSpread / Mathf.Pow(2, 1 / 6f);
            float eps = repulsionForce;
            float pidi = -8 * eps *
                         (2 * Mathf.Pow(sigma, 4) / Mathf.Pow(di, 5) - Mathf.Pow(sigma, 2) / Mathf.Pow(di, 3));
            Vector3 phi = (botT.position - transform.position).normalized;

            proxVec += pidi * phi;
        }

        return proxVec;
    }

    private Vector3 CalcAlignControl() {
        Vector3 alignVec = Vector3.zero;
        return alignVec;
    }

    private Vector3 CalcGoalControl() {
        Vector3 goalVec = Vector3.zero;
        return goalVec;
    }

    /// <summary>
    ///     Sets bot movement based on a vector, where vector magnitude controls bot speed.
    /// </summary>
    /// <param name="moveVector">Vector that points where the bot should go, with magnitude between 0 and 1</param>
    private void SetMoveByVector() {
        Debug.DrawRay(transform.position, moveVector / 2f, Color.green);

        //Slows us down for turns
        float fwdSpeedControl = Vector3.Dot(transform.forward, moveVector.normalized);
        //Controls the amount we slow down per turn
        fwdSpeedControl = Mathf.Lerp(moveVector.magnitude, moveVector.magnitude * fwdSpeedControl, turnSharpness);
        //Prevents stopping too fast
        fwdControl = Extensions.SharpInDamp(fwdControl, fwdSpeedControl, 1f);

        //Turns faster the larger the angle is, slows down as we approach desired heading
        float turnSpeedControl = Mathf.Deg2Rad * Vector3.Angle(transform.forward, moveVector);
        //Slows down turning if trying to turn too fast while moving too fast so we don't flip
        turnSpeedControl *= Mathf.Lerp(1f, .5f, Mathf.Lerp(0, fwdControl, turnSpeedControl / 3));

        //Decides which direction we should be turning
        float turnDirectionControl = Vector3.Dot(transform.right, moveVector.normalized);
        turnDirectionControl = turnDirectionControl / Mathf.Abs(turnDirectionControl + 0.01f);

        turnControl = turnDirectionControl * turnSpeedControl;
    }

    /// <summary>
    ///     Controls the bot movement. Should only be called once per physics timestep (last call will always override).
    /// </summary>
    /// <param name="fwdInput">Float between -1 and 1 that controls bots forward movement</param>
    /// <param name="turnInput">Float between -1 and 1 specifying how fast to turn. 1 is full right, -1 full left.</param>
    private void Move(float fwdInput, float turnInput) {
        foreach(WheelCollider wheel in wheelsR)
            wheel.motorTorque = fwdInput * speed - turnInput * turnSpeed;
        foreach(WheelCollider wheel in wheelsL)
            wheel.motorTorque = fwdInput * speed + turnInput * turnSpeed;
    }
}