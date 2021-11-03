using AquaSys.AquaEffect;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BloomSwitcher : MonoBehaviour
{
    public Volume volume;

    AquaBloom aquaBloom;
    Bloom bloom;

    private void Start()
    {
        VolumeProfile volumeProfile = volume.profile;

        var stack = VolumeManager.instance.stack;
        volumeProfile.TryGet(out aquaBloom);
        volumeProfile.TryGet(out bloom);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
    }

    public void SwitchToUnityBloom()
    {
        bloom.active = true;
        aquaBloom.active = false;
    }

    public void SwitchToAquaBloom()
    {
        bloom.active = false;

        aquaBloom.active = true;

    }

    public void SwitchFantasticBloom()
    {
        bloom.active = false;
        aquaBloom.active = true;
        aquaBloom.FantasticBloom.Override(!aquaBloom.FantasticBloom.value);
    }
}
