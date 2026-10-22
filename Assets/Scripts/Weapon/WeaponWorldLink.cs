using UnityEngine;

public class WeaponWorldLink : MonoBehaviour
{
    public GameObject worldModel;           
    public Transform worldMuzzle;           // Muzzle
    public ParticleSystem worldMuzzleFlash; // fogonazo dentro de Muzzle

    public void PlayWorldMuzzle()
    {
        if (worldMuzzleFlash == null) return;
        worldMuzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        worldMuzzleFlash.Play();
    }
}