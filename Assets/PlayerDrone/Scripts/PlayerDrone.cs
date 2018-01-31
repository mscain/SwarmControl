using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ExtensionMethods;

public class PlayerDrone : MonoBehaviour {
    #region Variables

    /*
     0 is null; no control, drones just drift around
     1 is carrot; carrot can be moved around scene, and swarm follows
     2 is vector based control at swarm COM
   TODO:
     3 can be vector based control at center of implied circle w/ optional visualization of the circle
     4 can be leader where carrot drags behind leader at distance based on size of swarm
        when leader turns, carrot will slowly move to keep being behind the leader
     5 can be the above, but the carrot won't stay behind the leader, so it's like the leader is dragging the carrot on a chain
     6 can be leader that rest of the swarm flock to -- leader holds the carrot
    */
    public int swarmControlMode = 1;

    public bool isControlled; //TODO make this switch to an auto-follow mode with a top down (and/or angled?) view

    //TODO this should auto be set by swarmControlMode, but should be able to be toggled manually too
    //TODO Make swarm spread changable
    public GameObject arrow, jet, jetBall;
    public LayerMask aimRayLayerMask;
    public ParticleSystem jetParticles;
    public GameObject cannon, barrel, laser;
    public LauncheeScript script;
    public bool glidey;
    public float glideySpeed = 15;
    public bool lowHover;
    public float maxWalkSpeed = 20f;
    public float kick = 50;
    public float sprintSpeed = 2.5f;
    public float slowSpeed = 0.3f;
    public float acceleration = 30f;
    public float accelerationHoriz = 30f;
    public float deceleration = 1;
    public float turnSpeed = 50;
    public float hoverHeight = 2;
    public float maxHoverHeight = 30;
    public float hoverForce = 65f;
    public float maxVerticalSpeed = 24;
    public float mass = 0.2f;
    public float maxPush = 750;
    public float minPush = 100;
    public float aimSpeed = 1;
    public float t = 2;
    public float shotSize = 0.2957171f;

    [HideInInspector] public float vertInput, horizInput, turnInput;

    public float cannonAngle;
    public float mouseSensitivity;
    public float push;
    private bool _shouldShoot;
    public float pGain = 2600; // the proportional gain
    public float iGain = 40; // the integral gain
    public float dGain = 260; // differential gain
    private float _integrator = 19.6f; // error accumulator
    private float _lastError;
    private float _curPos = 2; // actual Pos
    private float _force = 785; // current force
    private Rigidbody _rb;
    private PlayerDroneCamera _camera;
    private float _sprintVol1;
    private float _sprint = 1f;
    private float _velForward, _velRight;
    private Vector3 _fwdVec, _rightVec;
    private Transform _carrot;
    [HideInInspector] public List<GameObject> swarm;
    [HideInInspector] public Vector3 swarmVec, swarmCenterPos, swarmCenterDir;

    #endregion

    private void Awake() {
        _rb = GetComponent<Rigidbody>();
        float frac = (push - minPush) / (maxPush - minPush);
        barrel.GetComponent<MeshRenderer>().material.SetColor("_EmisColor", Color.HSVToRGB(1f, frac + .2f, 1));
        _carrot = GameObject.FindGameObjectWithTag("Carrot").transform;
        _camera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<PlayerDroneCamera>();
    }

    private void Update() {
        SwarmControlMode();

        if(isControlled) PlayerControl();
    }

    private void FixedUpdate() {
        JetControl();
        Walk();
    }

    private void LateUpdate() { UpdateJetBall(); }


    #region SwarmControl

    private void SwarmControlMode() {
        bool runOnce = false;

        for(int number = 0; number <= 9; number++) {
            if(!Input.GetKeyDown(number.ToString())) continue;
            swarmControlMode = number;
            runOnce = true;
        }

        switch(swarmControlMode) {
            case 1:
                if(runOnce) {
                    ActivateDrone();
                    Destroy(GetComponent<LineRenderer>());
                }

                MoveCarrot(Input.GetButton("Mouse0"));
                break;
            case 2:
                if(runOnce) {
                    DeactivateDrone();
                    Destroy(GetComponent<LineRenderer>());
                }

                SetSwarmCentersByCOM();
                ControlSwarmParallel();
                break;
            case 3:
                if(runOnce) DeactivateDrone();
                SetSwarmCentersByShape();
                ControlSwarmParallel();


                break;
            default:
                if(runOnce) ActivateDrone();
                break;
        }
    }

    private void ControlSwarmParallel() {
        AutoControl(controlRotation: true);

        arrow.transform.position = swarmCenterPos;
        arrow.transform.forward = _camera.transform.up;
        arrow.transform.localScale = new Vector3(1, 1, Input.GetAxis("Vertical"));

        Vector3 fwdControl = transform.forward * Input.GetAxis("Vertical");
        Vector3 rightControl = transform.right * Input.GetAxis("Horizontal");
        float zeroer = Mathf.Clamp(Input.GetAxis("Vertical") + Input.GetAxis("Horizontal"), 0f, 1f);
        swarmVec = Extensions.SharpInDamp(swarmVec, swarmVec * zeroer + fwdControl + rightControl, .5f);
        swarmVec = Vector3.ClampMagnitude(swarmVec, 1);
    }

    private void DeactivateDrone() {
        isControlled = false;
        laser.SetActive(false);
        arrow.SetActive(true);
        foreach(var mesh in GetComponentsInChildren<MeshRenderer>()) { mesh.enabled = false; }
        foreach(var coll in GetComponentsInChildren<Collider>()) { coll.enabled = false; }
    }

    private void ActivateDrone() {
        isControlled = true;
        laser.SetActive(true);
        arrow.SetActive(false);
        foreach(var mesh in GetComponentsInChildren<MeshRenderer>()) { mesh.enabled = true; }
        foreach(var coll in GetComponentsInChildren<Collider>()) { coll.enabled = true; }
    }

    private void SetSwarmCentersByCOM() {
        swarmCenterPos = Vector3.zero;
        swarmCenterDir = Vector3.zero;
        foreach(GameObject bot in swarm) {
            swarmCenterPos += bot.transform.position;
            swarmCenterDir += bot.transform.forward;
        }
        swarmCenterPos /= swarm.Count;
        swarmCenterDir /= swarm.Count;
        swarmCenterDir.Normalize();
    }

    private void SetSwarmCentersByShape() {
        swarmCenterPos = Vector3.zero;
        swarmCenterDir = Vector3.zero;
        List<Vector3> vertices = new List<Vector3>();
        foreach(GameObject bot in swarm) {
            vertices.Add(bot.transform.position);
            swarmCenterDir += bot.transform.forward;
        }
        List<Vector3> hull = JarvisMarchAlgorithm.GetConvexHull(vertices);

        LineRenderer lr = GetComponent<LineRenderer>() ? GetComponent<LineRenderer>() : gameObject.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Particles/Additive"));
        lr.widthMultiplier = 0.2f;
        lr.positionCount = hull.Count + 1;

        float sx = 0, sy = 0, sz = 0, sL = 0;
        for(int i = 0; i < hull.Count; i++) {
            lr.SetPosition(i, hull[i]);

            Vector3 prev = i == 0 ? hull.Last() : hull[i - 1];
            Vector3 curr = hull[i];
            float x0 = prev.x, y0 = prev.y, z0 = prev.z;
            float x1 = curr.x, y1 = curr.y, z1 = curr.z;
            float l = Vector3.Distance(prev, curr);
            sx += (x0 + x1) / 2 * l;
            sy += (y0 + y1) / 2 * l;
            sz += (z0 + z1) / 2 * l;
            sL += l;
        }
        lr.SetPosition(hull.Count, hull[0]);

        swarmCenterPos = new Vector3(sx / sL, sy / sL, sz / sL);
        swarmCenterDir /= swarm.Count;
        swarmCenterDir.Normalize();
    }

    private float GetSwarmRadius(Vector3 center) {
        float radius = 0;
        foreach(GameObject bot in swarm) {
            float distance = Vector3.Distance(bot.transform.position, center);
            if(distance > radius)
                radius = distance;
        }
        return radius;
    }

    /// <summary>
    /// Move the carrot to wherever the laser is pointing
    /// </summary>
    /// <param name="moveCarrotKey">Set to true to move carrot</param>
    private void MoveCarrot(bool moveCarrotKey) {
        if(!moveCarrotKey) return;

        RaycastHit hit;
        if(!Physics.Raycast(laser.transform.position, laser.transform.forward, out hit, 100f, aimRayLayerMask)) return;

        _carrot.transform.position = Extensions.SharpInDamp(_carrot.transform.position, hit.point, 1);
    }

    #endregion

    #region DroneAuto

    private void AutoControl(bool controlRotation) {
        //Position
        float swarmRadius = GetSwarmRadius(swarmCenterPos);
        Vector3 camPos = swarmCenterPos + Vector3.up * (-swarmCenterPos.y + transform.position.y);
        transform.position = Extensions.SharpInDamp(transform.position, camPos, 3f);

        //Rotation
        if(controlRotation) {
            _camera.smoothX = Input.GetAxis("Horizontal");
        } else {
            Vector3 camRot = Vector3.ProjectOnPlane(swarmCenterDir, Vector3.up);
            Vector3 forward = Vector3.ProjectOnPlane(_camera.transform.up, Vector3.up);
            float angle = Vector3.SignedAngle(forward, camRot, Vector3.up);
            if(Input.GetButton("Vertical") && !Input.GetButton("Horizontal"))
                angle = Mathf.Abs(angle) > 20 ? angle : 0;
            _camera.smoothX = Extensions.SharpInDampAngle(0, angle, .3f);
        }

        //Height
        float height = swarmRadius / Mathf.Tan(_camera.gameObject.GetComponent<Camera>().fieldOfView / 2 * Mathf.Deg2Rad) + 2.5f;
        hoverHeight = Extensions.SharpInDamp(hoverHeight, height, .3f);
    }

    #endregion

    #region DroneControlled

    private void PlayerControl() {
        if(Input.GetKeyDown("y")) {
            glidey = !glidey;
        }
        vertInput = Input.GetAxis("Vertical");
        horizInput = Input.GetAxis("Horizontal");

        Sprint(Input.GetButton("Shift"), Input.GetButton("Ctrl"));
        ShootBall(Input.GetKeyDown("f"));
        UpdateShootStrength(Input.GetAxis("Mouse ScrollWheel"));
        HoverControl(Input.GetKeyDown("u"), "E", "Q");

        _shouldShoot |= Input.GetButtonDown("Fire");
    }


    private void Walk() {
        if(glidey) {
            _rb.AddRelativeForce(horizInput * glideySpeed * _sprint, 0f, vertInput * glideySpeed * _sprint,
                ForceMode.Acceleration);
            _rb.AddRelativeTorque(0f, turnInput * turnSpeed * mouseSensitivity, 0f, ForceMode.Acceleration);
        } else {
            _velForward = Vector3.Dot(_rb.velocity, transform.forward); //Current fwd speed. Used to see if Kick is necessary.
            _velRight = Vector3.Dot(_rb.velocity, transform.right); //Similar to ^

            _fwdVec = transform.forward * acceleration * vertInput * Time.fixedDeltaTime * _sprint;
            _rightVec = transform.right * accelerationHoriz * horizInput * Time.fixedDeltaTime * _sprint;
            if(Mathf.Abs(vertInput) > 0 && Mathf.Abs(horizInput) > 0) {
                if(Mathf.Abs(_velForward) < maxWalkSpeed * _sprint / 1.5f) {
                    _rb.velocity += _fwdVec;
                }

                if(Mathf.Abs(_velRight) < maxWalkSpeed * _sprint / 1.5f) {
                    _rb.velocity += _rightVec;
                }
            } else {
                if(Mathf.Abs(_velForward) < maxWalkSpeed * _sprint) {
                    _rb.velocity += _fwdVec;
                    if(Mathf.Abs(horizInput) < 0.1f)
                        _rb.velocity -= transform.right * _velRight * deceleration / 5;
                }

                if(Mathf.Abs(_velRight) < maxWalkSpeed * _sprint) {
                    _rb.velocity += _rightVec;
                    if(Mathf.Abs(vertInput) < 0.1f)
                        _rb.velocity -= transform.forward * _velForward * deceleration / 5;
                }
            }

            //::Kick - If pressing walk from standstill, gives a kick so walking is more responsive.
            if(vertInput > 0 && _velForward < maxWalkSpeed / 3) {
                _rb.AddRelativeForce(0, 0, kick * 30 * _sprint);
                //Debug.Log("kickf"); //\\\\\\\\\\\\\\\\\\\\\\\\\\
            } else if(vertInput < 0 && _velForward > -maxWalkSpeed / 3) {
                _rb.AddRelativeForce(0, 0, -kick * 30 * _sprint);
                //Debug.Log("kickb"); //\\\\\\\\\\\\\\\\\\\\\\\\\\
            }

            if(horizInput > 0 && _velRight < maxWalkSpeed / 3) {
                _rb.AddRelativeForce(kick * 30 * _sprint, 0, 0);
                //Debug.Log("kickr"); //\\\\\\\\\\\\\\\\\\\\\\\\\\
            } else if(horizInput < 0 && _velRight > -maxWalkSpeed / 3) {
                _rb.AddRelativeForce(-kick * 30 * _sprint, 0, 0);
                //Debug.Log("kickl"); //\\\\\\\\\\\\\\\\\\\\\\\\\\
            }
        }
    }

    private void Sprint(bool sprintPressed, bool slowPressed) {
        if(sprintPressed) {
            _sprint = sprintSpeed;
        } else if(slowPressed) {
            _sprint = slowSpeed;
        } else {
            _sprint = Mathf.SmoothDamp(_sprint, 1, ref _sprintVol1, 0.2f);
        }
    }

    private void HoverControl(bool changeHoverMode, string hoverUpAxisName, string hoverDownAxisName) {
        if(changeHoverMode) {
            if(lowHover) {
                hoverHeight = transform.position.y;
            } else {
                RaycastHit hit;
                if(Physics.Raycast(transform.position, -transform.up, out hit, maxHoverHeight)) {
                    hoverHeight = hit.distance;
                }
            }

            lowHover = !lowHover;
        }

        if(Input.GetButton(hoverUpAxisName)) {
            var ps = jetParticles.velocityOverLifetime;
            ps.z = 6 * _sprint;
        } else if(Input.GetButton(hoverDownAxisName)) {
            var ps = jetParticles.velocityOverLifetime;
            ps.z = -6 * _sprint;
        } else if(Input.GetButtonUp(hoverUpAxisName) || Input.GetButtonUp(hoverDownAxisName)) {
            var ps = jetParticles.velocityOverLifetime;
            ps.z = 0;
        }

        hoverHeight += (Input.GetAxis(hoverUpAxisName) - Input.GetAxis(hoverDownAxisName)) *
                       Mathf.Pow(Mathf.Max(Mathf.Min(hoverHeight, 20), 1), 0.6f) / (40) * _sprint;
        if(lowHover) {
            hoverHeight = Mathf.Clamp(hoverHeight, 0.3f, maxHoverHeight);
        }
    }

    private void ShootBall(bool shootKey) {
        if(!shootKey && !_shouldShoot) return;
        _shouldShoot = false;
        LauncheeScript shot = Instantiate(script, cannon.transform.position, gameObject.transform.rotation);
        shot.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        shot.script = GetComponent<PlayerDrone>();
        shot.barrelT = barrel.transform;
        shot.push = push;
        shot.size = shotSize;
        shot.t = t;
        shot.aimSpeed = aimSpeed;
        shot.gameObject.GetComponent<Rigidbody>().mass = mass;
    }

    private void UpdateShootStrength(float shootStrengthAxis) {
        if(!(Mathf.Abs(shootStrengthAxis) > 0.01f)) return;
        push += shootStrengthAxis * 400;
        push = Mathf.Clamp(push, minPush, maxPush);

        float frac = (push - minPush) / (maxPush - minPush);
        barrel.GetComponent<MeshRenderer>().material.SetColor("_EmisColor", Color.HSVToRGB(1f, frac + .2f, 1));
    }

    #endregion

    #region DroneGeneral

    private void JetControl() {
        Quaternion jbr = Quaternion.FromToRotation(-transform.up, Vector3.down);
        float downDot = Vector3.Dot(transform.up, Vector3.down);
        float rightDot = Vector3.Dot(transform.right, Vector3.down);
        if(downDot > -0.3f && downDot < 0.3f) {
            jbr = Quaternion.Euler(-transform.up) *
                  Quaternion.AngleAxis(-50 * rightDot / Mathf.Abs(rightDot), transform.forward);
        } else if(downDot > 0.3f) {
            jbr = Quaternion.Euler(Vector3.right) * Quaternion.AngleAxis(30, transform.right);
        }

        jet.transform.rotation = jbr;
        jetBall.transform.rotation = Quaternion.Slerp(jetBall.transform.rotation, jbr,
            Quaternion.Angle(jetBall.transform.rotation, jbr) * Time.fixedDeltaTime / 10);

        Vector3 jetDir = -jet.transform.up;
        Vector3 posVec = transform.position - jet.transform.position;
        _rb.AddTorque(-Vector3.Cross(jetDir, posVec) * hoverForce * 3);
        if(!(Vector3.Dot(jet.transform.up, Vector3.down) < .5)) return; //Break if the jet is pointing up
        if(lowHover) {
            Ray ray = new Ray(transform.position, -transform.up);
            RaycastHit hit;
            if(Physics.Raycast(ray, out hit, hoverHeight)) {
                float proportionalHeight = (hoverHeight - hit.distance) / hoverHeight;
                Vector3 appliedHoverForce = jet.transform.up * proportionalHeight * hoverForce;
                _rb.AddForce(appliedHoverForce, ForceMode.Acceleration);
            } else {
                _rb.AddForce(hoverForce * 20 * Vector3.down);
            }
        } else {
            _curPos = transform.position.y;
            float error = hoverHeight - _curPos; //generate the error signal
            _integrator += error * Time.fixedDeltaTime; //integrate error
            float diff = (error - _lastError) / Time.fixedDeltaTime; //differentiate error
            _lastError = error;
            //calculate the force summing the 3 errors with respective gains:
            _force = error * pGain + _integrator * iGain + diff * dGain;
            if(Mathf.Abs(Vector3.Dot(_rb.velocity, transform.up)) < maxVerticalSpeed) {
                _rb.AddForce(_force * jet.transform.up); // apply the force to accelerate the rigidbody
            }
        }
    }

    private void UpdateJetBall() {
        if(!isControlled) return;
        jetBall.transform.Rotate(transform.right * vertInput * 6, Space.World);
        jetBall.transform.Rotate(transform.forward * -horizInput * 6, Space.World);
    }

    #endregion
}