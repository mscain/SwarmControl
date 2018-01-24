using System.Diagnostics;
using UnityEngine;
using ExtensionMethods;

public class PlayerDrone : MonoBehaviour {
    #region Variables

    public int controlMode = 1; //0 is null, 1 is Carrot, 2 is TODO...
    public GameObject thisCamera, jet, jetBall;
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
    public bool isControlled;

    [HideInInspector] public float vertInput, horizInput, turnInput;

//	[HideInInspector]
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
    private float _sprintVol1;
    private float _sprint = 1f;
    private float _velForward, _velRight;
    private Vector3 _fwdVec, _rightVec;
    private Transform _carrot;

    #endregion

    private void Awake() {
        _rb = GetComponent<Rigidbody>();
        float frac = (push - minPush) / (maxPush - minPush);
        barrel.GetComponent<MeshRenderer>().material.SetColor("_EmisColor", Color.HSVToRGB(1f, frac + .2f, 1));
        _carrot = GameObject.FindGameObjectWithTag("Carrot").transform;
    }

    private void Update() {
        SetControlMode();

        if(!isControlled) return;
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

    private void FixedUpdate() {
        MoveCarrot(Input.GetButton("Mouse0"));
        JetControl();
        Walk();
    }

    private void LateUpdate() { UpdateJetBall(); }

    private void SetControlMode() {
        for(int number = 0; number <= 9; number++) {
            if(Input.GetKeyDown(number.ToString()))
                controlMode = number;
        }
        //TODO set things and such
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

    private void HoverControl(bool changeHoverMode, string hoverUpAxisName, string hoverDownAxisName) {
        if(changeHoverMode) {
            if(lowHover) {
                hoverHeight = transform.position.y;
            } else {
                Ray ray = new Ray(transform.position, -transform.up);
                RaycastHit hit;
                if(Physics.Raycast(ray, out hit, maxHoverHeight)) {
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

    private void UpdateJetBall() {
        if(!isControlled) return;
        jetBall.transform.Rotate(transform.right * vertInput * 6, Space.World);
        jetBall.transform.Rotate(transform.forward * -horizInput * 6, Space.World);
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
}