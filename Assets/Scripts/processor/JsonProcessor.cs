using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public abstract class JsonProcessor<T> : Processor
{
    //private int msgId;



   /* public JsonProcessor(int msgId) {
        this.msgId = msgId;
    }*/

    public void Process(byte[] msg)
    {
        string str = System.Text.Encoding.Default.GetString(msg);
        try
        {
            T msgObj = JsonManager.Instance.Deserialize<T>(str);

            Process(msgObj);
        }
        catch (Exception e) { 
            Debug.LogException(e);
            Debug.Log("msg : " + str + " | make exception , " + typeof(T));
        }
        
    }


    public abstract void Process(T msgObj);



}


