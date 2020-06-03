using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeBarValue : MonoBehaviour
{
    // Start is called before the first frame update
    public ValueScript valueScript;
    private EnergyBar bar;
    void Start()
    {
         bar = GetComponent<EnergyBar>();
      //   valueScript = GetComponent<ValueScript>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ChangeBarValueMethod()
    {
        bar.ValueF = valueScript.value/100;
    }
}
