using UnityEngine;
using System.Collections;

public class CarSounds : MonoBehaviour
{
    public float minSpeed = 0f;
    public float maxSpeed = 8f; // Set a reasonable max speed to start with
    public float minPitch = 0.5f;
    public float maxPitch = 4.0f;

    private Rigidbody carRB;
    private AudioSource carAudio;

    void Start()
    {
        carRB = GetComponent<Rigidbody>();
        carAudio = GetComponent<AudioSource>();
    }

    void Update()
    {
        EngineSound();
    }

    void EngineSound()
    {
        float currentSpeed = carRB.linearVelocity.magnitude;

        float normalizedSpeed = Mathf.InverseLerp(minSpeed, maxSpeed, currentSpeed);

        float targetPitch = Mathf.Lerp(minPitch, maxPitch, normalizedSpeed);

        carAudio.pitch = Mathf.Lerp(carAudio.pitch, targetPitch, Time.deltaTime * 5f);
    }
}