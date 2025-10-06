using UnityEngine;
using System;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
    public enum Axel { Front, Rear }

    [Serializable]
    public struct Wheel
    {
        public GameObject wheelModel;
        public WheelCollider wheelCollider;
        public GameObject wheelEffectObj;
        public ParticleSystem smokeParticle;
        public Axel axel;
    }

    [Header("Motor Settings")]
    [Tooltip("Engine torque power. Higher = faster acceleration.")]
    public float maxMotorTorque = 15000f; // 🚀 way more power
    public float accelerationMultiplier = 3.5f;

    [Header("Braking Settings")]
    public float maxBrakeTorque = 10000f;
    public float idleBrakeTorque = 80f;

    [Header("Steering Settings")]
    public float maxSteerAngle = 30f;
    public float steeringSmoothness = 10f;

    [Header("Speed Settings")]
    [Tooltip("Top speed in km/h (approx feel, not literal).")]
    public float maxSpeed = 380f;
    public bool limitSpeed = true;

    [Header("Physics Settings")]
    public Vector3 centerOfMass = new Vector3(0, -0.6f, 0);
    public float downforce = 80f;

    [Header("Friction Settings")]
    [Tooltip("Lower = more slip and faster acceleration, higher = more grip.")]
    public float forwardStiffness = 1.2f;
    public float sidewaysStiffness = 1.6f;

    [Header("Visuals")]
    public List<Wheel> wheels;

    private Rigidbody carRB;
    private float moveInput;
    private float steerInput;

    void Start()
    {
        carRB = GetComponent<Rigidbody>();
        carRB.centerOfMass = centerOfMass;

        // Reduce drag to go faster
        carRB.linearDamping = 0.005f;
        carRB.angularDamping = 0.015f;

        // Tune friction for more acceleration & better corner grip
        foreach (var wheel in wheels)
        {
            WheelFrictionCurve forwardFriction = wheel.wheelCollider.forwardFriction;
            forwardFriction.extremumSlip = 0.5f;
            forwardFriction.extremumValue = 1.3f;
            forwardFriction.asymptoteSlip = 0.8f;
            forwardFriction.asymptoteValue = 1.0f;
            forwardFriction.stiffness = forwardStiffness;
            wheel.wheelCollider.forwardFriction = forwardFriction;

            WheelFrictionCurve sidewaysFriction = wheel.wheelCollider.sidewaysFriction;
            sidewaysFriction.extremumSlip = 0.4f;
            sidewaysFriction.extremumValue = 1.2f;
            sidewaysFriction.asymptoteSlip = 0.7f;
            sidewaysFriction.asymptoteValue = 0.9f;
            sidewaysFriction.stiffness = sidewaysStiffness;
            wheel.wheelCollider.sidewaysFriction = sidewaysFriction;
        }
    }

    void Update()
    {
        GetInputs();
        AnimateWheels();
    }

    void FixedUpdate()
    {
        Move();
        Steer();
        ApplyBrakes();
        ApplyDownforce();
        WheelEffects();
    }

    void GetInputs()
    {
        moveInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");
    }

    void Move()
{
    float currentSpeed = carRB.linearVelocity.magnitude * 3.6f; // m/s → km/h

    // Limit top speed if enabled
    if (limitSpeed && currentSpeed > maxSpeed)
    {
        foreach (var wheel in wheels)
            wheel.wheelCollider.motorTorque = 0f;
        return;
    }

    // Smooth torque scaling for consistent feel
    float speedFactor = Mathf.Clamp01(currentSpeed / maxSpeed);
    float torqueBoost = Mathf.Lerp(2.5f, 0.8f, speedFactor) * accelerationMultiplier;

    // ✅ Stop torque when no input
    float appliedTorque = 0f;
    if (Mathf.Abs(moveInput) > 0.01f) // deadzone
        appliedTorque = moveInput * maxMotorTorque * torqueBoost;

    foreach (var wheel in wheels)
    {
        if (wheel.axel == Axel.Rear && wheel.wheelCollider.isGrounded)
            wheel.wheelCollider.motorTorque = appliedTorque;
    }
}

void ApplyBrakes()
{
    float currentBrakeTorque = 0f;
    float forwardVelocity = Vector3.Dot(carRB.linearVelocity, transform.forward);

    // Strong brake if space pressed or reversing
    if (Input.GetKey(KeyCode.Space) ||
        (moveInput < 0 && forwardVelocity > 0.1f) ||
        (moveInput > 0 && forwardVelocity < -0.1f))
    {
        currentBrakeTorque = maxBrakeTorque;
    }
    else if (Mathf.Approximately(moveInput, 0f))
    {
        // Slight braking when idle to slow naturally
        currentBrakeTorque = idleBrakeTorque;

        // Add engine braking for realism
        foreach (var wheel in wheels)
        {
            if (wheel.axel == Axel.Rear)
                wheel.wheelCollider.motorTorque = 0f; // stop giving torque
        }
    }

    foreach (var wheel in wheels)
        wheel.wheelCollider.brakeTorque = currentBrakeTorque;
}


    void Steer()
    {
        float targetSteerAngle = steerInput * maxSteerAngle;

        foreach (var wheel in wheels)
        {
            if (wheel.axel == Axel.Front)
            {
                wheel.wheelCollider.steerAngle = Mathf.Lerp(
                    wheel.wheelCollider.steerAngle,
                    targetSteerAngle,
                    steeringSmoothness * Time.deltaTime
                );
            }
        }
    }

    void AnimateWheels()
    {
        foreach (var wheel in wheels)
        {
            wheel.wheelCollider.GetWorldPose(out _, out Quaternion rot);
            wheel.wheelModel.transform.rotation = rot; // rotation only
        }
    }

    void ApplyDownforce()
    {
        carRB.AddForce(-transform.up * downforce * carRB.linearVelocity.magnitude);
    }

    void WheelEffects()
{
    foreach (var wheel in wheels)
    {
        if (!Input.GetKey(KeyCode.Space)) // only check when handbraking
        {
            var trail = wheel.wheelEffectObj?.GetComponentInChildren<TrailRenderer>();
            if (trail != null) trail.emitting = false;
            if (wheel.smokeParticle != null && wheel.smokeParticle.isPlaying)
                wheel.smokeParticle.Stop();
            continue;
        }

        if (wheel.wheelCollider.GetGroundHit(out WheelHit hit))
        {
            bool isSlipping = Mathf.Abs(hit.forwardSlip) > 0.2f || Mathf.Abs(hit.sidewaysSlip) > 0.2f;

            var trail = wheel.wheelEffectObj?.GetComponentInChildren<TrailRenderer>();
            if (trail != null) trail.emitting = isSlipping;

            if (wheel.smokeParticle != null)
            {
                if (isSlipping && !wheel.smokeParticle.isPlaying)
                    wheel.smokeParticle.Play();
                else if (!isSlipping && wheel.smokeParticle.isPlaying)
                    wheel.smokeParticle.Stop();
            }
        }
    }
}

}


