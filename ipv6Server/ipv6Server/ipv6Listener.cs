using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ipv6Server
{
    class ipv6Listener
    {
        const int PORT = 1979;
        Socket serverListener;
        const int MAXBUFF = 1024 * 1024 * 200;
        const int CMDBUFF = 1024 * 20;
        const int REQBUFF = 1024 * 20;
        byte[] dataBuff;

        Dictionary<string, string> accountList;
        Dictionary<string, Socket> serviceList;

        public ipv6Listener() 
        {
            accountList = new Dictionary<string, string>();
            serviceList = new Dictionary<string, Socket>();
            dataBuff = new byte[MAXBUFF];
            if (!File.Exists("users"))
            {
                File.Create("users").Close();
            }

            string[] users = File.ReadAllLines("users");
            foreach (string account in users)
            {
                string[] ba = account.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                accountList.Add(ba[0], ba[1]);
            }
        }
        /// <summary>
        /// 给每一个新登录的用户创建的线程
        /// </summary>
        /// <param name="name">用户名</param>
        private void PointService(object name)
        {
            //获取与用户对应的socket索引
            string username = (string)name;
            Socket skt = serviceList[username];

            //指令缓冲区
            byte[] reqBytes = new byte[REQBUFF];
            int reqLength;
            string req;
            string cmd;
                string targetUser;
            //数据长度
            int dataLength;

            while (true)
            {
                try
                {
                    reqLength = skt.Receive(reqBytes);
                    req = DataFilter.GetString(reqBytes, 0, reqLength);

                    if (req == null)
                    {
                        continue;
                    }

                    string[] reqs = req.Split("$".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    cmd = reqs[0];
                    targetUser = reqs[1];

                    if (cmd.Equals("cmd::sendmessage"))
                    {
                        string targetMsg = reqs[2];
                        Console.WriteLine("接收到来自{0}的转发消息请求", username);
                        Console.WriteLine("目标用户: {0}", targetUser);
                        Console.WriteLine("要转发的消息内容{0} :", targetMsg);

                        //找到目标用户的socket
                        if (serviceList.ContainsKey(targetUser))
                        {
                            Socket targetSocket = serviceList[targetUser];

                            //发送指令和数据
                            targetSocket.Send(DataFilter.GetBytes("cmd::receivemessage$" + targetMsg));
                            Console.WriteLine("已向目标用户发送消息");
                            Console.WriteLine();
                        }
                        else
                        {
                            Console.WriteLine("向{0}发送消息失败", targetUser);
                            Console.WriteLine();
                        }
                        continue;
                    }
                    else if (cmd.Equals("cmd::sendfile"))
                    {
                        string fileName = reqs[2];

                        Console.WriteLine("接收到{0}的发送文件请求", username);
                        Console.WriteLine("目标用户: {0}", targetUser);
                        Console.WriteLine("文件名:{0}", fileName);

                        //接收文件
                        dataLength = skt.Receive(dataBuff);
                        byte[] fileBuff = new byte[dataLength];
                        Array.Copy(dataBuff, fileBuff, dataLength);
                        Console.WriteLine("接收到文件 文件大小: {0}Bytes", dataLength);

                        if (serviceList.ContainsKey(targetUser))
                        {
                            //找到目标用户的socket
                            Socket tgskt = serviceList[targetUser];

                            //先发送参数 命令 发送者 和 文件名
                            tgskt.Send(DataFilter.GetBytes("cmd::receivefile$" + username + "$" + fileName));
                            Console.WriteLine("已向目标用户发送接收文件指令,发送者,和文件名");

                            Thread.Sleep(100);

                            //发送文件
                            tgskt.Send(fileBuff);
                            Console.WriteLine("已向目标用户发送文件");
                            Console.WriteLine();
                            continue;
                        }
                        else
                        {
                            Console.WriteLine("发送失败 可能是由于目标用户已下线");
                            continue;
                        }

                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERROR:{0}:{1}", username, e.Message);
                    serviceList.Remove(username);
                    Console.WriteLine("已将{0}从服务列表中移除", username);
                    Thread.CurrentThread.Abort();
                }
            }
        }

        public void StartService()
        {
            if (!Socket.OSSupportsIPv6)
            {
                Console.Error.WriteLine("系统不支持ipv6");
                return;
            }

            serverListener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            serverListener.Bind(new IPEndPoint(IPAddress.IPv6Any, PORT));
            serverListener.Listen(0);


            Console.WriteLine("服务器已启动，正在监听...\n");

            while (true)
            {
                Console.WriteLine("\r\nWaiting for incoming connections on " + PORT);
                //接收用户连接请求
                Socket socket = serverListener.Accept();
                Console.WriteLine("1 socket accepted");
                
                byte[] reqBytes = new byte[REQBUFF];

                int reqLength;
                //接收用户提供的指令和账号密码

                reqLength = socket.Receive(reqBytes);

                string req = DataFilter.GetString(reqBytes, 0, reqLength);
                string[] reqs = req.Split("$".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                Console.WriteLine("收到的请求数据:{0}", req);
                string command = reqs[0];
                string username = reqs[1];
                string password = reqs[2];

                Console.WriteLine("command: {0}", reqs[0]);
                Console.WriteLine("username: {0}", reqs[1]);
                Console.WriteLine("password: {0}", reqs[2]);

                //若用户请求注册
                if (command.Equals("cmd::register"))
                {
                    try
                    {
                        accountList.Add(username, password);
                        socket.Send(DataFilter.GetBytes("cmd::added"));
                        Console.WriteLine("cmd::added");
                        File.AppendAllText("users", username + " " + password + "\r\n");
                    }
                    catch
                    {
                        socket.Send(DataFilter.GetBytes("cmd::registerfailed"));
                        Console.WriteLine("cmd::registerfailed");
                    }
                    continue;
                }
                else if (command.Equals("cmd::login"))
                {
                    //这个账户存在
                    if (accountList.ContainsKey(username) && password == accountList[username])
                    {
                        Console.WriteLine("存在此账户");
                        if (serviceList.ContainsKey(username))
                        {
                            Console.WriteLine("duplicate user");
                            socket.Send(DataFilter.GetBytes("cmd::loginfailed"));
                            continue;
                        }

                        socket.Send(DataFilter.GetBytes("cmd::success"));

                        //添加到socket列表
                        serviceList.Add(username, socket);

                        //以下开启新的线程为用户服务
                        Thread t = new Thread(new ParameterizedThreadStart(PointService));
                        t.IsBackground = true;
                        t.Start((object)username);
                        Console.WriteLine("为{0}的服务已开启", username);
                        
                        //通知所有用户更新在线用户列表
                        string onlineList = string.Empty;
                        foreach(KeyValuePair<string,Socket> kvp in serviceList)
                        {
                            onlineList += (kvp.Key + "$");
                        }
                        foreach (KeyValuePair<string, Socket> kvp in serviceList)
                        {
                            kvp.Value.Send(DataFilter.GetBytes("cmd::getonlinelist$" + onlineList));
                        }
                        continue;
                    }
                    else
                    {
                        //这个账户不存在
                        socket.Send(DataFilter.GetBytes("cmd::loginfailed"));
                        continue;
                    }
                }
            }
        }
    }
}
