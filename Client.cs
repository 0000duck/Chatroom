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

namespace Chatroom_Client
{
    public partial class Client : Form
    {
        private Socket clientSocket = null;
        private Thread receiveThread;
        private IPAddress serverIPAdd; // 存放服务器的IP地址
        private int serverPortNum; // 存放服务器的端口号
        private string clientName; // 存放本客户端的名称
        private bool isListening;
        private List<string> onlineUserList; // 存放当前网络上的在线客户端名
        private Dictionary<string, List<string>> history; // 存放聊天记录
        private string clientToSend; // 存放要发送信息的目标客户端名

        public Client()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false; // 启用跨线程访问
            onlineUserList = new List<string>();
            clientToSend = "all"; // 初始状态为群发消息
            sendTarget.Text = "所有人";
            history = new Dictionary<string, List<string>>();
            history.Add("all", new List<string>()); // 保存与所有人的聊天记录
        }

        private void Client_Load(object sender, EventArgs e)
        {

        }

        private void connect_Click(object sender, EventArgs e) // 连接服务器
        {
            if (String.IsNullOrWhiteSpace(serverIP.Text))
            {
                messages.Text += "服务器IP地址不能为空\r\n";
                return;
            }
            else if (String.IsNullOrWhiteSpace(serverPort.Text))
            {
                messages.Text += "服务器端口号不能为空\r\n";
                return;
            }
            else if (String.IsNullOrWhiteSpace(name.Text))
            {
                messages.Text += "客户端昵称不能为空\r\n";
                return;
            }
            if (clientSocket == null)
            {
                try
                {
                    serverIPAdd = IPAddress.Parse(serverIP.Text);
                    serverPortNum = Int32.Parse(serverPort.Text);
                }
                catch
                {
                    messages.Text += "IP地址或端口号不合法，请重新输入\r\n";
                    return;
                }
                try
                {
                    clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); // 创建与服务器相连接的socket
                    clientSocket.Connect(new IPEndPoint(serverIPAdd, serverPortNum)); // 连接到服务器
                }
                catch
                {
                    messages.Text += "连接服务器失败，请检查服务器是否已开启\r\n";
                    clientSocket = null;
                    return;
                }
                clientName = name.Text;
                clientSocket.Send(Encoding.UTF8.GetBytes(clientName)); // 向服务器发送本客户端的昵称
                receiveThread = new Thread(receiveMessage);
                receiveThread.IsBackground = true;
                receiveThread.Start();
                isListening = true;
            }
            else
            {
                messages.Text += "请先断开当前连接的服务器后重试\r\n";
            }
        }

        private void disconnect_Click(object sender, EventArgs e) // 断开服务器
        {
            try
            {
                if (clientSocket != null)
                {
                    onlineUserList = new List<string>(); // 清空在线客户端列表
                    onlineUsers.Items.Clear();
                    string disconnectMessage = "disconnect\n" + clientName;
                    clientSocket.Send(Encoding.UTF8.GetBytes(disconnectMessage));
                    receiveThread.Abort(); // 关闭接收消息的线程
                    isListening = false;
                    clientSocket = null;
                    messages.Text += DateTime.Now.ToString() + " 成功断开服务器" + "\r\n";
                }
                else
                {
                    messages.Text += "未连接到服务器，无法断开\r\n";
                }
            }
            catch
            {
                onlineUserList = new List<string>(); // 清空在线客户端列表
                onlineUsers.Items.Clear();
                receiveThread.Abort(); // 关闭接收消息的线程
                isListening = false;
                clientSocket = null;
                messages.Text += "服务器故障，已断开连接\r\n";
            }
        }

        private void sendMessage_Click(object sender, EventArgs e)
        {
            send();
        }

        private void receiveMessage() // 接收消息
        {
            isListening = true;
            try
            {
                while (isListening)
                {
                    Byte[] bytesFrom = new Byte[1000];
                    if (clientSocket != null && clientSocket.Connected)
                    {
                        Int32 len = clientSocket.Receive(bytesFrom);
                        String receiveStr = Encoding.UTF8.GetString(bytesFrom, 0, len);
                        if (!String.IsNullOrWhiteSpace(receiveStr))
                        {
                            if (receiveStr == "Server has stopped") // 服务器关闭
                            {
                                clientSocket.Close();
                                clientSocket = null;
                                messages.Text += DateTime.Now.ToString() + " 服务器已关闭" + "\r\n";
                                receiveThread.Abort();
                                return;
                            }
                            else if (receiveStr == "昵称已被占用，请重新输入")
                            {
                                messages.Text += receiveStr + "\r\n";
                                isListening = false;
                                clientSocket.Close();
                                clientSocket = null;
                            }
                            else if (receiveStr == "已成功连接服务器")
                            {
                                messages.Text += DateTime.Now.ToString() + " " + receiveStr + "\r\n";
                            }
                            string[] receiveStrArray = receiveStr.Split('\n'); // 按照换行符拆分
                            if (receiveStrArray[0] == "message") // 普通消息
                            {
                                string sourceClient = receiveStrArray[1];
                                string sendTarget = receiveStrArray[2];
                                string sendContent = receiveStrArray[3];
                                string finalMessage;
                                if (sendTarget == "all") // 群发消息
                                {
                                    finalMessage = DateTime.Now.ToString() + " " + sourceClient + "对所有人说: " + sendContent + "\r\n";
                                    history[sendTarget].Add(finalMessage); // 添加到消息记录中
                                }
                                else // 私聊消息
                                {
                                    finalMessage = DateTime.Now.ToString() + " " + sourceClient + "对你说：" + sendContent + "\r\n";
                                    history[sourceClient].Add(finalMessage); // 添加到消息记录中
                                }
                                messages.Text += finalMessage;
                            }
                            else if (receiveStrArray[0] == "refresh") // 更新本地的在线客户端列表
                            {
                                // 清空客户端列表
                                onlineUserList = new List<string>();
                                onlineUsers.Items.Clear();
                                for (int i = 1; i < receiveStrArray.Length; i++)
                                {
                                    if (!history.ContainsKey(receiveStrArray[i])) // 新加入的客户端
                                    {
                                        history.Add(receiveStrArray[i], new List<string>());
                                    }
                                    onlineUserList.Add(receiveStrArray[i]);
                                    onlineUsers.Items.Add(receiveStrArray[i]);
                                }
                            }
                            else if (receiveStrArray[0] == "file") // 文件传输
                            {
                                string sourceClient = receiveStrArray[1];
                                byte[] fileCache = new byte[1024 * 1024 * 10]; // 定义10M的缓冲区
                                messages.Text += sourceClient + "希望给你发送文件" + receiveStrArray[3] + "\r\n";
                                try
                                {
                                    int fileLength = clientSocket.Receive(fileCache);
                                    if (saveFileDialog1.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                                    {
                                        string fileSavePath = saveFileDialog1.FileName;// 获得文件保存的路径
                                        using (FileStream fs = new FileStream(fileSavePath, FileMode.Create)) // 创建文件流，然后根据路径创建文件
                                        {
                                            fs.Write(fileCache, 0, fileLength);
                                            messages.Text += "文件已成功保存在" + fileSavePath + "\r\n";
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
                }
            }
            catch (SocketException ex)
            {
                isListening = false;
                if (clientSocket != null && clientSocket.Connected)
                {
                    onlineUserList = new List<string>(); // 清空在线客户端列表
                    onlineUsers.Items.Clear();
                    string disconnectMessage = "disconnect\n" + clientName;
                    clientSocket.Send(Encoding.UTF8.GetBytes(disconnectMessage));
                    receiveThread.Abort(); // 关闭接收消息的线程
                    isListening = false;
                    clientSocket = null;
                }
            }
        }

        private void Client_FormClosing(object sender, FormClosingEventArgs e) // 关闭窗口时，断开服务器
        {
            try
            {
                if (clientSocket != null)
                {
                    onlineUserList = new List<string>(); // 清空在线客户端列表
                    onlineUsers.Items.Clear();
                    string disconnectMessage = "disconnect\n" + clientName;
                    clientSocket.Send(Encoding.UTF8.GetBytes(disconnectMessage));
                    receiveThread.Abort(); // 关闭接收消息的线程
                    isListening = false;
                    clientSocket = null;
                    messages.Text += DateTime.Now.ToString() + " 已断开服务器" + "\r\n";
                }
            }
            catch
            {
                onlineUserList = new List<string>(); // 清空在线客户端列表
                onlineUsers.Items.Clear();
                receiveThread.Abort(); // 关闭接收消息的线程
                isListening = false;
                clientSocket = null;
                messages.Text += DateTime.Now.ToString() + " 服务器故障，已断开连接\r\n";
            }
        }

        private void onlineUsers_ItemActivate(object sender, EventArgs e) // 双击在线用户中的某个用户，切换到对应的聊天窗口
        {
            clientToSend = onlineUsers.FocusedItem.Text;
            if (clientToSend == "all")
            {
                sendTarget.Text = "所有人";
            }
            else
            {
                sendTarget.Text = clientToSend;
            }
            messages.Clear();
            List<string> chattingHistory = history[clientToSend];
            for (int i = 0; i < chattingHistory.Count; i++)
            {
                messages.Text += chattingHistory[i];
            }
        }

        private void messageToSend_KeyDown(object sender, KeyEventArgs e) // 输入框按下回车发送消息
        {
            if (e.KeyCode == Keys.Enter)
            {
                send();
            }
        }

        private void send() // 发送消息
        {
            string message = messageToSend.Text;
            if (String.IsNullOrWhiteSpace(message))
            {
                messages.Text += "发送内容不能为空\r\n";
            }
            else if (clientSocket != null && clientSocket.Connected)
            {
                string finalMessage = "message\n" + clientName + "\n" + clientToSend + "\n" + message;
                clientSocket.Send(Encoding.UTF8.GetBytes(finalMessage));
                if (clientToSend != "all")
                {
                    finalMessage = "你对" + clientToSend + "说：" + message + " " + DateTime.Now.ToString() + "\r\n";
                }
                else
                {
                    finalMessage = "你对所有人说：" + message + " " + DateTime.Now.ToString() + "\r\n";
                }
                history[clientToSend].Add(finalMessage);
                messages.Text += finalMessage;
                messageToSend.Text = "";
            }
            else
            {
                messages.Text += "请先连接服务器\r\n";
            }
        }

        private void selectFile_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                System.IO.StreamReader sr = new
                   System.IO.StreamReader(openFileDialog1.FileName);
                filePath.Text = openFileDialog1.FileName; // 显示所选择的文件名
                sr.Close();
            }
        }

        private void sendFile_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(filePath.Text))
            {
                messages.Text += "请选择你要发送的文件\r\n";
            }
            else
            {
                if (clientSocket != null)
                {
                    // 用文件流打开用户要发送的文件
                    using (FileStream fs = new FileStream(filePath.Text, FileMode.Open))
                    {
                        string fileName = System.IO.Path.GetFileName(filePath.Text);
                        string fileExtension = System.IO.Path.GetExtension(filePath.Text);
                        string fileMessage = "file\n" + clientName + "\n" + clientToSend + "\n" + fileName + "\n" + fileExtension;
                        clientSocket.Send(Encoding.UTF8.GetBytes(fileMessage)); // 告知服务器要进行文件传输  
                        byte[] arrFile = new byte[1024 * 1024 * 10]; // 10M的文件缓冲区
                        fs.Read(arrFile, 0, arrFile.Length); // 将文件中的数据读到arrFile数组中
                        clientSocket.Send(arrFile);
                        filePath.Clear();
                    }
                }
                else
                {
                    messages.Text += "请先连接服务器\r\n";
                }
            }
        }
    }
}
