using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEditor;
using System;

public static class UnityExecuteOutExe
{    
    // Use this for initialization
    static public void Run(object obj)
    {
        Thread t = new Thread(RunExe);
        t.Start(obj);
    }


    static void RunExe(object obj)
    {
        string fileNameAndParameter = null;
        if (obj is string)
            fileNameAndParameter = obj as string;
        else
            return;

        string fileName = fileNameAndParameter.Split('&')[0];
        string command = fileNameAndParameter.Split('&')[1];

        using (Process p = new Process())
        {
            p.StartInfo = new ProcessStartInfo();
            p.StartInfo.FileName = fileName;  //确定程序名
            p.StartInfo.Arguments = command;  //指定程式命令行
            p.StartInfo.UseShellExecute = false;   //是否使用Shell
            p.StartInfo.RedirectStandardInput = true;   //重定向输入
            p.StartInfo.RedirectStandardOutput = true;   //重定向输出
            p.StartInfo.RedirectStandardError = true;    //重定向输出错误
            p.StartInfo.CreateNoWindow = false;        //设置不显示窗口
            p.Start();

            p.WaitForExit();

            //return p.StandardOutput.ReadToEnd();     //输出流取得命令行结果
        }
    }


}
