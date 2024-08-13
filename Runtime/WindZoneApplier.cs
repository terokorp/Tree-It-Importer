using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(WindZone))]
public class WindZoneApplier : MonoBehaviour
{
    private WindZone _wz;
    readonly static int _windDirectionID = Shader.PropertyToID("_WindDirection");
    readonly static int _windID = Shader.PropertyToID("_Wind");

    // Start is called before the first frame update
    void Start()
    {
        _wz = GetComponent<WindZone>();
    }

    // Update is called once per frame
    void Update()
    {
        if (_wz == null)
            return;

        Shader.SetGlobalVector(_windDirectionID, _wz.transform.forward);
        Shader.SetGlobalFloatArray(_windID, new float[] {
            _wz.windMain,
            _wz.windTurbulence,
            _wz.windPulseMagnitude,
            _wz.windPulseFrequency
        });
    }

    private void OnEnable()
    {
        Shader.EnableKeyword("_WIND");        
    }

    private void OnDisable()
    {
        Shader.DisableKeyword("_WIND");

        Shader.SetGlobalVector(_windDirectionID, Vector3.zero);
        Shader.SetGlobalFloatArray(_windID, new float[] {
            0,
            0,
            0,
            0
        });
    }
}
