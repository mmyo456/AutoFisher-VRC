﻿using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace VRChatAutoFishing
{
    public partial class HelpForm : Form
    {
        public HelpForm()
        {
            InitializeComponent();
            SetupHelpForm();
        }

        private void SetupHelpForm()
        {
            this.Text = "使用说明 - 自动钓鱼v1.5.0";
            this.ClientSize = new Size(610, 950); // 增加高度以容纳第四步
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            Panel mainPanel = new Panel();
            mainPanel.Dock = DockStyle.Fill;
            mainPanel.AutoScroll = true;
            mainPanel.BackColor = Color.White;
            this.Controls.Add(mainPanel);

            // 作者名称
            Label authorLabel = new Label();
            authorLabel.Location = new Point(0, 20);
            authorLabel.Size = new Size(580, 30);
            authorLabel.Text = "作者：arcxingye 欢迎加我玩哦~";
            authorLabel.Font = new Font("微软雅黑", 14, FontStyle.Bold);
            authorLabel.TextAlign = ContentAlignment.MiddleCenter;
            authorLabel.ForeColor = Color.DarkBlue;
            mainPanel.Controls.Add(authorLabel);

            int currentY = 60;

            // 加载三张图片
            LoadImageWithFallback(mainPanel, "help_image1.png", "1.打开OSC", 1064, 341, ref currentY);
            LoadImageWithFallback(mainPanel, "help_image2.png", "2.打开日志", 1064, 745, ref currentY);
            LoadImageWithFallback(mainPanel, "help_image3.png", "3.把鱼钩静置到桶上", 1064, 745, ref currentY);

            // 第四步 - 只有文字说明
            Label step4Label = new Label();
            step4Label.Location = new Point(25, currentY);
            step4Label.Size = new Size(550, 25);
            step4Label.Text = "4.打开软件的开始按钮";
            step4Label.Font = new Font("微软雅黑", 11, FontStyle.Bold);
            step4Label.TextAlign = ContentAlignment.MiddleLeft;
            mainPanel.Controls.Add(step4Label);

            currentY += 40;

            // 第四步详细说明
            Label step4DescLabel = new Label();
            step4DescLabel.Location = new Point(25, currentY);
            step4DescLabel.Size = new Size(550, 60);
            step4DescLabel.Text = "通常，做完这些就可以开始自动钓鱼了，可以后台挂着VRC玩别的游戏去。软件可能会随着世界更新而失效，注意更新，没有更新要么懒得要么真没办法了。本软件以合法性为主，仅仅只是个OSC程序，非常安全，不存在风险。";
            step4DescLabel.Font = new Font("微软雅黑", 10, FontStyle.Regular);
            step4DescLabel.TextAlign = ContentAlignment.TopLeft;
            mainPanel.Controls.Add(step4DescLabel);

            currentY += 80;

            // 美化关闭按钮
            Button closeButton = new Button();
            closeButton.Location = new Point(250, currentY + 20);
            closeButton.Size = new Size(100, 35);
            closeButton.Text = "关闭";

            // 美化按钮样式
            closeButton.BackColor = Color.SteelBlue;
            closeButton.ForeColor = Color.White;
            closeButton.Font = new Font("微软雅黑", 10, FontStyle.Bold);
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.LightSteelBlue;
            closeButton.FlatAppearance.MouseDownBackColor = Color.RoyalBlue;

            // 添加阴影效果（通过边框模拟）
            closeButton.FlatAppearance.BorderColor = Color.DarkSlateBlue;

            // 添加圆角效果（需要自定义绘制）
            closeButton.Paint += (sender, e) =>
            {
                Button btn = (Button)sender;
                using (Pen pen = new Pen(Color.DarkSlateBlue, 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, btn.Width - 1, btn.Height - 1);
                }
            };

            closeButton.Click += (s, e) => this.Close();
            mainPanel.Controls.Add(closeButton);
        }

        private void LoadImageWithFallback(Panel parent, string imageName, string description, int originalWidth, int originalHeight, ref int currentY)
        {
            int displayWidth = 550;
            int displayHeight = (int)((double)originalHeight / originalWidth * displayWidth);

            // 说明文字
            Label descLabel = new Label();
            descLabel.Location = new Point(25, currentY);
            descLabel.Size = new Size(displayWidth, 25);
            descLabel.Text = description;
            descLabel.Font = new Font("微软雅黑", 11, FontStyle.Bold);
            descLabel.TextAlign = ContentAlignment.MiddleLeft;
            parent.Controls.Add(descLabel);

            currentY += 30;

            // 图片容器
            Panel imageContainer = new Panel();
            imageContainer.Location = new Point(25, currentY);
            imageContainer.Size = new Size(displayWidth, displayHeight);
            imageContainer.BorderStyle = BorderStyle.FixedSingle;
            imageContainer.BackColor = Color.White;
            parent.Controls.Add(imageContainer);

            PictureBox pictureBox = new PictureBox();
            pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBox.Dock = DockStyle.Fill;

            bool imageLoaded = false;

            // 方法1: 尝试从嵌入式资源加载
            if (!imageLoaded)
            {
                imageLoaded = LoadImageFromEmbeddedResource(pictureBox, imageName);
            }

            // 方法2: 尝试从文件系统加载
            if (!imageLoaded)
            {
                imageLoaded = LoadImageFromFileSystem(pictureBox, imageName);
            }

            // 方法3: 如果都失败，显示错误图片
            if (!imageLoaded)
            {
                ShowErrorImage(pictureBox, displayWidth, displayHeight, $"未找到图片: {imageName}");
            }

            imageContainer.Controls.Add(pictureBox);
            currentY += displayHeight + 20;
        }

        private bool LoadImageFromEmbeddedResource(PictureBox pictureBox, string imageName)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();

                // 尝试不同的资源名称格式
                string[] possibleResourceNames = {
                    $"VRChatAutoFishing.Resources.{imageName}",
                    $"VRChatAutoFishing.{imageName}",
                    $"Resources.{imageName}",
                    imageName
                };

                foreach (string resourceName in possibleResourceNames)
                {
                    using (var stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null)
                        {
                            pictureBox.Image = Image.FromStream(stream);
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // 忽略错误，继续尝试其他方法
            }
            return false;
        }

        private bool LoadImageFromFileSystem(PictureBox pictureBox, string imageName)
        {
            try
            {
                // 尝试从应用程序目录加载
                string[] possiblePaths = {
                    Path.Combine(Application.StartupPath, "Resources", imageName),
                    Path.Combine(Application.StartupPath, imageName),
                    Path.Combine(Directory.GetCurrentDirectory(), "Resources", imageName),
                    Path.Combine(Directory.GetCurrentDirectory(), imageName)
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        pictureBox.Image = Image.FromFile(path);
                        return true;
                    }
                }
            }
            catch
            {
                // 忽略错误
            }
            return false;
        }

        private void ShowErrorImage(PictureBox pictureBox, int width, int height, string errorMessage)
        {
            Bitmap errorImage = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(errorImage))
            {
                g.Clear(Color.LightGray);
                using (Font font = new Font("微软雅黑", 9))
                using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(errorMessage, font, Brushes.Red, new RectangleF(0, 0, width, height), sf);
                }
            }
            pictureBox.Image = errorImage;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "HelpForm";
            this.ResumeLayout(false);
        }
    }
}