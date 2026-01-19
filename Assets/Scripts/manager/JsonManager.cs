using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class JsonManager
{

    private static JsonManager instance = new JsonManager();

    private JsonManager() { }

    public static JsonManager Instance
    {
        get
        {
            //if (instance == null)
            //{
            //    instance = new JsonManager();
            //}
            return instance;
        }
    }


    public string Serialize(object obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    public T Deserialize<T>(string obj)
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(obj);
        }
        catch (Exception e)
        {
            Debug.Log(" Deserialize exception  : " + obj + " type :" + typeof(T));
            throw e;
        }


    }
}