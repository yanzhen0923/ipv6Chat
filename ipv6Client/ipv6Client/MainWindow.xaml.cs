using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace ipv6Client
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        Socket connection;
        const int MAXBUFF = 1024 * 1024 * 200;
        const int CMDBUFF = 100;
        const int REQBUFF = 1024 * 20;
        DESCryptoServiceProvider DESalg; 

        public MainWindow()
        {
            if (!Socket.OSSupportsIPv6)
            {
                MessageBox.Show("您的系统暂不支持ipv6");
                return;
            }
            InitializeComponent();

            DESalg = new DESCryptoServiceProvider();
        }

        private void GetMessageFromServerThread()
        {
            byte[] reqBuff = new byte[REQBUFF];
            int reqLength;
            string req;

            string cmd;

            byte[] dataBuff = new byte[MAXBUFF];
            int dataLength;

            string fromUser;
            string fromFileName;

            while (true)
            {
                try
                {
                    reqLength = connection.Receive(reqBuff);
                    req = DataFilter.GetString(reqBuff, 0, reqLength);

                    if (req == null)
                        continue;

                    string[] reqs = req.Split("$".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    cmd = reqs[0];

                    if (cmd.Equals("cmd::getonlinelist"))
                    {
                        Dispatcher.Invoke(new Action(() =>
                            {
                                onlineListBox.Items.Clear();
                                for(int i = 1; i < reqs.Length; ++ i)
                                {
                                    if (reqs[i] != userTextBox.Text)
                                    {
                                        onlineListBox.Items.Add(reqs[i]);
                                    }
                                }
                            }
                        ));
                        continue;
                    }
                    else if (cmd.Equals("cmd::receivemessage"))
                    {
                        Dispatcher.Invoke(new Action(() =>
                            {
                                msgTextBox.AppendText(reqs[1] + "\r\n");
                            }
                        ));
                        continue;
                    }
                    else if (cmd.Equals("cmd::receivefile"))
                    {
                        fromUser = reqs[1];
                        fromFileName = reqs[2];

                        //MessageBox.Show(fromFileName);
                        dataLength = connection.Receive(dataBuff);
                        //MessageBox.Show(dataLength.ToString());

                        byte[] newFileBuff = new byte[dataLength];
                        Array.Copy(dataBuff, newFileBuff, dataLength);

                        
                        Dispatcher.Invoke(() =>
                        {
                            string writePath;
                            if (Directory.Exists(receiveFileTextbox.Text))
                            {
                                writePath = receiveFileTextbox.Text + "\\" + fromFileName;
                            }
                            else
                                writePath = "F:\\" + fromFileName;

                            File.WriteAllBytes(writePath, newFileBuff);

                            fileMsgBox.AppendText(fromUser + " 向您发送了一个文件: " + fromFileName + "\r\n"
                                + "文件大小 : " + dataLength.ToString() + "\r\n"
                                    + "已保存至" + writePath + "\r\n");
                        }
                        );
                        continue;
                    }
                }
                catch(Exception e)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        MessageBox.Show(e.Message);
                        generalGrid.IsEnabled = true;
                    }));
                    Thread.CurrentThread.Abort();
                }
            }
        }

        private void conButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IPAddress ipa = IPAddress.Parse(servetIPTextBox.Text);
                IPEndPoint ipeh = new IPEndPoint(ipa, int.Parse(portTextBox.Text));
                connection = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                connection.Connect(ipeh);

                connection.Send(DataFilter.GetBytes("cmd::login$" + userTextBox.Text + "$" + passTextBox.Password));

                //获取服务器验证信息
                byte[] buff = new byte[CMDBUFF];
                int len = connection.Receive(buff);
                string verify = DataFilter.GetString(buff, 0, len);
                if (verify == "cmd::success")
                {
                    generalGrid.IsEnabled = false;
                    Thread t = new Thread(new ThreadStart(GetMessageFromServerThread));
                    t.IsBackground = true;
                    t.Start();
                    MessageBox.Show("登录成功");
                }
                else
                {
                    MessageBox.Show("登录失败");
                }
            }
            catch
            {
                MessageBox.Show("登录参数不正确或无法连接到服务器");
            }
        }

        private void regButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IPAddress ipa = IPAddress.Parse(servetIPTextBox.Text);
                IPEndPoint ipeh = new IPEndPoint(ipa, int.Parse(portTextBox.Text));
                connection = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                connection.Connect(ipeh);

                connection.Send(DataFilter.GetBytes("cmd::register$" + userTextBox.Text + "$" + passTextBox.Password));

                byte[] buff = new byte[CMDBUFF];
                int len = connection.Receive(buff);
                MessageBox.Show(DataFilter.GetString(buff, 0, len));
            }
            catch
            {
                MessageBox.Show("登录参数不正确或无法连接到服务器");
            }
        }

        private void sendMsgButton_Click(object sender, RoutedEventArgs e)
        {
            if (toSendTextBox.Text == string.Empty)
            {
                MessageBox.Show("消息不能为空");
                return;
            }

            if (onlineListBox.SelectedItem == null)
            {
                MessageBox.Show("请选择发送目标");
                return;
            }

            string targetUser = (string)onlineListBox.SelectedItem;

            connection.Send(DataFilter.GetBytes("cmd::sendmessage$"
                + targetUser + "$"
                + "来自" + userTextBox.Text + "的消息: " + toSendTextBox.Text)); 

                msgTextBox.AppendText("发给 " + targetUser + " 的消息: " + toSendTextBox.Text + "\r\n");

                toSendTextBox.Clear();
        }

        private void sendFileButton_Click(object sender, RoutedEventArgs e)
        {
            
            if (onlineListBox.SelectedItem == null)
            {
                MessageBox.Show("请选择发送目标");
                return;
            }

            string targetUser = (string)onlineListBox.SelectedItem;

            OpenFileDialog op = new OpenFileDialog();
            op.RestoreDirectory = true;

            if (op.ShowDialog() == true)
            {
                byte[] fileBytes = File.ReadAllBytes(op.FileName);

                //发详细参数
                connection.Send(DataFilter.GetBytes("cmd::sendfile$" + targetUser + "$" + op.SafeFileName));

                Thread.Sleep(100);

                //发送文件
                connection.Send(fileBytes);

                fileMsgBox.AppendText("向 " + targetUser + " 发送了一个文件: " + op.FileName + "\r\n"); 

            }
        }

        private void decryptFile_Click(object sender, RoutedEventArgs e)
        {
            string key = keyTextBox.Text;
            if (key.Length != 8)
            {
                MessageBox.Show("密钥长度不正确");
                return;
            }

            if (!File.Exists(inputFilePathTextBox.Text))
            {
                MessageBox.Show("输入文件路径不正确");
                return;
            }

            try
            {

                if (!Directory.Exists(Path.GetDirectoryName(outputFilePathTextBox.Text)))
                {
                    MessageBox.Show("输出文件路径不存在");
                    return;
                }

                DESalg.Key = Encoding.ASCII.GetBytes(key);
                DESalg.IV = Encoding.ASCII.GetBytes(key);

                byte[] toDecrypt = File.ReadAllBytes(inputFilePathTextBox.Text);
                byte[] Decrypted = FileFilter.DecryptTextFromMemory(toDecrypt, DESalg.Key, DESalg.IV);
                File.WriteAllBytes(outputFilePathTextBox.Text, Decrypted);

                MessageBox.Show("解密文件保存至" + outputFilePathTextBox.Text, "文件已解密");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void encryptFile_Click(object sender, RoutedEventArgs e)
        {
            string key = keyTextBox.Text;
            if (key.Length != 8)
            {
                MessageBox.Show("密钥长度不正确");
                return;
            }
            if (!File.Exists(inputFilePathTextBox.Text))
            {
                MessageBox.Show("输入文件路径不正确");
                return;
            }

            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(outputFilePathTextBox.Text)))
                {
                    MessageBox.Show("输出文件路径不存在");
                    return;
                }

                DESalg.Key = Encoding.ASCII.GetBytes(key);
                DESalg.IV = Encoding.ASCII.GetBytes(key);

                byte[] toEncrypt = File.ReadAllBytes(inputFilePathTextBox.Text);
                byte[] Encrypted = FileFilter.EncryptTextToMemory(toEncrypt, DESalg.Key, DESalg.IV);
                File.WriteAllBytes(outputFilePathTextBox.Text, Encrypted);

                MessageBox.Show("加密文件保存至" + outputFilePathTextBox.Text, "文件已加密");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            
        }

        private void selectInputFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog();
            op.RestoreDirectory = true;
            if (op.ShowDialog() == true)
            {
                inputFilePathTextBox.Text = op.FileName;
            }
        }

        private void selectOutputFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog op = new OpenFileDialog();
            op.RestoreDirectory = true;
            if (op.ShowDialog() == true)
            {
                outputFilePathTextBox.Text = op.FileName;
            }
        }
    }
}
