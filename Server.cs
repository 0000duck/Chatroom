using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;

namespace Chatroom_Server
{
    public partial class Server : Form
    {
        private IPAddress serverIP; // 存放服务器的IP
        private int serverPort; // 存放服务器的端口号
        private Socket serverSocket; // 服务端运行的SOCKET
        private Thread serverThread; // 服务端运行的线程
        public Dictionary<string, Socket> clientSockets; // 为客户端建立的SOCKET连接
        private IPEndPoint endPoint;
        bool isListening;

        public Server()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false; // 启用跨线程访问
        }

        private void Server_Load(object sender, EventArgs e)
        {
            
        }

        private void startServer_Click(object sender, EventArgs e) // 启动服务器
        {
            if (serverSocket == null)
            {
                try
                {
                    isListening = true;
                    clientSockets = new Dictionary<string, Socket>();
                    try
                    {
                        if (String.IsNullOrWhiteSpace(IP.Text))
                        {
                            messages.Text += "IP地址不能为空\r\n";
                            return;
                        }
                        else if (String.IsNullOrWhiteSpace(port.Text))
                        {
                            messages.Text += "端口号不能为空\r\n";
                            return;
                        }
                        try
                        {
                            serverIP = IPAddress.Parse(IP.Text);
                            serverPort = Int32.Parse(port.Text);
                            endPoint = new IPEndPoint(serverIP, serverPort);
                            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            serverSocket.Bind(endPoint);  //绑定IP地址：端口
                        }
                        catch
                        {
                            messages.Text += "IP地址或端口号不合法，请重新输入\r\n";
                            serverSocket = null;
                            return;
                        }
                        serverSocket.Listen(10);    //设定最多10个排队连接请求
                        serverThread = new Thread(recieveAccept);
                        serverThread.IsBackground = true; // 将服务器线程设置为与后台同步，随着主线程结束而结束 
                        serverThread.Start(); // 启动监听客户端连接的线程
                        messages.BeginInvoke(new Action(() =>
                        {
                            messages.Text += DateTime.Now.ToString() + " 成功启动服务器" + "\r\n";
                        }));
                    }
                    catch
                    {
                        messages.BeginInvoke(new Action(() =>
                        {
                            messages.Text += "服务器启动失败\r\n";
                        }));
                        if (serverSocket != null)
                        {
                            serverSocket.Close();
                            serverThread.Abort(); // 关闭服务器监听线程
                            foreach (var socket in clientSockets.Values)
                            {
                                socket.Close();
                            }
                            clientSockets.Clear();
                            serverSocket = null;
                            isListening = false;
                        }
                    }
                }
                catch (SocketException ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
            else
            {
                messages.Text += "服务器已在运行\r\n";
            }
        }

        private void stopServer_Click(object sender, EventArgs e) // 停止服务器
        {
            if (serverSocket != null)
            {
                serverSocket.Close();
                serverThread.Abort(); // 关闭监听线程
                foreach (var socket in clientSockets.Values) // 关闭与客户端连接的所有socket
                {
                    socket.Send(Encoding.UTF8.GetBytes("Server has stopped"));
                    socket.Close();
                }
                clientSockets.Clear();
                serverSocket = null;
                isListening = false;
                onlineUsers.Items.Clear();
                messages.Text += DateTime.Now.ToString() + " 服务器已停止" + "\r\n";
            }
            else
            {
                messages.Text += "未启动服务器，无法断开\r\n";
            }
        }

        private void recieveAccept() // 接受新的客户端连接
        {
            isListening = true;
            Socket clientSocket = default(Socket);
            while (isListening)
            {
                try
                {
                    if (serverSocket == null) // 如果服务器停止，serverSocket为空，直接返回
                    {
                        return;
                    }
                    clientSocket = serverSocket.Accept(); // 等待接受客户端连接
                }
                catch (SocketException ex)
                {
                    MessageBox.Show(ex.ToString());
                }
                Byte[] bytesFrom = new Byte[1000];
                if (clientSocket != null && clientSocket.Connected)
                {
                    try
                    {
                        Int32 len = clientSocket.Receive(bytesFrom); //获取客户端发来的信息,返回的就是收到的字节数,并且把收到的信息都放在bytesForm里面
                        if (len > -1)
                        {
                            String clientName = Encoding.UTF8.GetString(bytesFrom, 0, len); // 将字节流转换成字符串
                            if (!clientSockets.ContainsKey(clientName)) // 客户端昵称可用
                            {
                                clientSockets.Add(clientName, clientSocket); // 将该client对应的socket添加到所有socket的字典中
                                HandleClient client = new HandleClient(this, messages, onlineUsers);
                                client.StartClient(clientSocket, clientName);
                                messages.BeginInvoke(new Action(() =>
                                {
                                    messages.Text += DateTime.Now.ToString() + " " + clientName + "连接上了服务器" + "\r\n";
                                }));
                                string successMessage = "已成功连接服务器";
                                clientSocket.Send(Encoding.UTF8.GetBytes(successMessage));
                                refreshSockets();
                            }
                            else
                            {
                                clientSocket.Send(Encoding.UTF8.GetBytes("昵称已被占用，请重新输入"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                }
            }
        }

        private void sendMessage_Click(object sender, EventArgs e)
        {
            send();
        }

        public void refreshSockets() // 刷新服务器的在线客户端列表，并发送至各个客户端，令其更新各自的在线客户端列表
        {
            string refreshMessage = "refresh\n";
            // 清空客户端列表
            onlineUsers.Items.Clear();
            foreach(KeyValuePair<string, Socket> pair in clientSockets) // 更新服务器的在线客户端列表，并构造更新命令
            {
                refreshMessage += pair.Key + '\n';
                onlineUsers.Items.Add(pair.Key);
            }
            onlineUsers.Items.Add("all");
            refreshMessage += "all\n"; // 群聊也作为一个客户端
            foreach (KeyValuePair<string, Socket> pair in clientSockets) // 将更新命令发送到各个客户端
            {
                pair.Value.Send(Encoding.UTF8.GetBytes(refreshMessage));
            }
        }

        private void Server_FormClosing(object sender, FormClosingEventArgs e) // 关闭服务器端窗口
        {
            if (serverSocket != null)
            {
                foreach (var socket in clientSockets.Values) // 关闭与客户端连接的所有socket
                {
                    socket.Send(Encoding.UTF8.GetBytes("Server has stopped"));
                    socket.Close();
                }
                clientSockets.Clear();
                serverSocket.Close();
                serverThread.Abort();
                serverSocket = null;
                isListening = false;
                messages.Text += DateTime.Now.ToString() + " 服务器停止，已断开所有客户端连接" + "\r\n";
            }
        }

        private void messageToSend_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                send();
            }
        }

        private void send()
        {
            string message = messageToSend.Text;
            if (String.IsNullOrWhiteSpace(message))
            {
                messages.Text += "发送内容不能为空\r\n";
            }
            else if (serverSocket != null)
            {
                string finalMessage = "message\n服务器\nall\n" + message;
                foreach (KeyValuePair<string, Socket> pair in clientSockets)
                {
                    Socket targetClientSocket = pair.Value;
                    targetClientSocket.Send(Encoding.UTF8.GetBytes(finalMessage));
                }
                messages.Text = DateTime.Now.ToString() + " " + messages.Text + "服务器: " + message + "\r\n";
                messageToSend.Text = "";
            }
            else
            {
                messages.Text += "请先启动服务器\r\n";
            }
        }
    }

    public class HandleClient
    {
        Server server;
        Socket clientSocket;
        string clientName;
        TextBox messages;
        ListView onlineUsers;
        public HandleClient() { }
        public HandleClient(Server s, TextBox tb, ListView lv)
        {
            server = s;
            messages = tb;
            onlineUsers = lv;
        }
        public void StartClient(Socket inClientSocket, string cName)
        {
            clientSocket = inClientSocket;
            clientName = cName;
            Thread th = new Thread(Chat);
            th.IsBackground = true;
            th.Start();
        }
        private void Chat() // 客户端聊天处理
        {
            bool isListening = true;
            while (isListening)
            {
                try
                {
                    Byte[] receiveBytes = new byte[1000];
                    int receiveLen = clientSocket.Receive(receiveBytes);
                    if (receiveLen > -1)
                    {
                        string receiveStr = Encoding.UTF8.GetString(receiveBytes, 0, receiveLen);
                        if (!String.IsNullOrWhiteSpace(receiveStr))
                        {
                            string[] receiveStrArray = receiveStr.Split('\n');
                            if (receiveStrArray[0] == "message") // 普通消息。receiveStrArray[1]为发送客户端，receiveStrArray[2]为接收客户端，receiveStrArray[3]为内容
                            {
                                string sourceClient = receiveStrArray[1];
                                string sendTarget = receiveStrArray[2];
                                string sendContent = receiveStrArray[3];
                                if (sendTarget == "all") // 消息群发
                                {
                                    foreach (KeyValuePair<string, Socket> pair in server.clientSockets)
                                    {
                                        if (pair.Key != clientName) // 不发送回源客户端
                                        {
                                            Socket targetClientSocket = pair.Value;
                                            targetClientSocket.Send(Encoding.UTF8.GetBytes(receiveStr));
                                        }
                                    }
                                    messages.Text = messages.Text + sourceClient + ": " + sendContent + "\r\n";
                                }
                                else // 单独发送给某一客户端
                                {
                                    Socket targetClientSocket = server.clientSockets[sendTarget];
                                    targetClientSocket.Send(Encoding.UTF8.GetBytes(receiveStr));
                                }
                            }
                            else if (receiveStrArray[0] == "disconnect") // 客户端断开连接
                            {
                                isListening = false;
                                server.clientSockets.Remove(clientName); // 从所有socket中删除该客户端对应的socket
                                messages.BeginInvoke(new Action(() =>
                                {
                                    messages.Text += DateTime.Now.ToString() +" " + clientName + "已断开与服务器连接 " + "\r\n";
                                }));
                                clientSocket.Shutdown(SocketShutdown.Both);
                                clientSocket.Close();
                                clientSocket = null;
                                server.refreshSockets(); // 刷新在线客户端列表
                            }
                            else if (receiveStrArray[0] == "file") // 文件传输
                            {
                                byte[] fileCache = new byte[1024 * 1024 * 10]; // 10M的文件缓冲区
                                int fileLength = clientSocket.Receive(fileCache); // 收取客户端发来的文件
                                if (receiveStrArray[2] == "all") // 群发文件
                                {
                                    foreach (KeyValuePair<string, Socket> pair in server.clientSockets) // 不发送回源客户端
                                    {
                                        if (pair.Key != clientName)
                                        {
                                            Socket targetClientSocket = pair.Value;
                                            targetClientSocket.Send(Encoding.UTF8.GetBytes(receiveStr));
                                            byte[] fileArr = new byte[fileLength];
                                            Buffer.BlockCopy(fileCache, 0, fileArr, 0, fileLength);
                                            targetClientSocket.Send(fileArr);
                                        }
                                    }
                                }
                                else // 单独发送文件
                                {
                                    string sourceClient = receiveStrArray[1];
                                    string sendTarget = receiveStrArray[2];
                                    Socket targetClientSocket = server.clientSockets[sendTarget];
                                    targetClientSocket.Send(Encoding.UTF8.GetBytes(receiveStr));
                                    byte[] fileArr = new byte[fileLength];
                                    Buffer.BlockCopy(fileCache, 0, fileArr, 0, fileLength);
                                    targetClientSocket.Send(fileArr);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    server.clientSockets.Remove(clientName);
                    clientSocket.Close();
                    clientSocket = null;
                    server.refreshSockets(); // 刷新在线客户端列表
                }
            }
        }
    }
}
