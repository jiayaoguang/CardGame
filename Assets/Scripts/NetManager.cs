using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEditor.VersionControl;
using UnityEngine;

public class NetManager : MonoBehaviour
{
    // Start is called before the first frame update


    public string ip = "127.0.0.1";
    public int port = 8080;


    private static NetManager netManager;

    public static NetManager Instance
    {
        get
        {
            return netManager;
        }
    }



    private NetClient netClient;

    public NetClient GetNetClient
    {
        get
        {
            return netClient;
        }
    }


    void Start()
    {
        UnityEngine.Debug.Log("before start .................");
        netClient = new TcpNetClient();

        netManager = this;


        netClient.StartClient(ip, port);

        UnityEngine.Debug.Log("start .................");


        InitAllMsg();


        //netClient.Send(new LotteryMsg());
    }



    private void InitAllMsg() 
    {

        netClient.RegisterProto(2000 , typeof(LotteryMsg));
        netClient.RegisterProto(2001, typeof(LotteryMsg));
    }

    // Update is called once per frame
    void Update()
    {
        netClient.Update();

    }
}
