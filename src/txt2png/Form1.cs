using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;

namespace txt2png
{
    public partial class Form1 : Form
    {
        RenderText rt = new RenderText();　// 文字列描画クラス

        string messageFormat = "[$num] $user/$message"; // メッセージの書式(メッセージあり)
        string messageFormatNoMessage = "[$num] $user"; // メッセージなし

        string fileName = @".\data.csv"; // 読み込みCSVファイル名
        int defaultXStep = 4; // メッセージ間の空白の初期値
        int defaultYStep = 21; // メッセージ間の行間隔

        int offsetCount = 1; // メッセージの開始番号

        int defaultWidth = 12000;
        int defaultHeight = 8000;

        // 後から設定する値
        int lines = 0; // レンダリングした行数
        int stringHeight = 0; // 1行あたりの高さの最大値
        string[] data; // csvファイルの内容 (1行目はヘッダ)
        Font currentFont = new Font("ＭＳ ゴシック", 18f, FontStyle.Bold); // デフォルトフォント

        List<string> _str = new List<string>(); // メッセージ本文
        List<int> _strWidth = new List<int>(); // メッセージ幅

        // 実行状態
        bool isRunning = false;

        // コンストラクタ
        public Form1()
        {
            InitializeComponent();
            fontDialog1.Font = currentFont;
            this.textBoxMessage.Text = messageFormat;
            this.textBoxNoMessage.Text = messageFormatNoMessage;
            this.textBoxWidth.Text = defaultWidth.ToString();
            this.textBoxHeight.Text = defaultHeight.ToString();
            this.textBoxOffset.Text = offsetCount.ToString();
            if (File.Exists(fileName))
            {
                data = File.ReadAllLines(fileName, Encoding.UTF8);
                RedrawSample();
            }
            else
            {
                MessageBox.Show(fileName + "ファイルが見つかりませんでした。", "読み込みエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.button2.Enabled = false;
                Application.Exit();
            }
        }


        // 出力ボタン
        private void button2_Click(object sender, EventArgs e)
        {
            if (isRunning)
            {
                isRunning = false;
                this.button2.Text = "生成";
                this.progressBar1.Value = 0;
                MessageBox.Show("中止しました",
                        "出力失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            this.button2.Enabled = false;
            isRunning = true;
            this.button2.Text = "中止";

            int width = defaultWidth;
            int height = defaultHeight;

            // レンダリングサイズの決定
            try
            {
                width = Convert.ToInt32(this.textBoxWidth.Text.Trim());
                height = Convert.ToInt32(this.textBoxHeight.Text.Trim());
            }
            catch
            {
                width = defaultWidth;
                height = defaultHeight;
            }
            finally
            {
                if (width > 16383 || width < 1) width = 16383;
                if (height > 16383 || height < 1) height = 16383;
                this.textBoxWidth.Text = width.ToString();
                this.textBoxHeight.Text = height.ToString();
            }
            int s = defaultXStep;

            // 文字サイズの計測
            this.statusLabel.Text = "メッセージの文字幅を計測しています...";
            GetAllTextSize(data, currentFont);

            // レイアウトの推定
            float spacing = 0;
            s--;
            do
            {
                s++;
                lines = height; // 初期値
                int u = PlanLayout(width, height, s, null);
                spacing = u / (float)width;
                this.statusLabel.Text = "配置を最適化しています (空白率: " +(100 * spacing).ToString() + " %)";
            } while (spacing > 0.1 || spacing < 0.01);

            this.button2.Enabled = true;
            this.statusLabel.Text = "レンダリングしています...";

            // レンダリング
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.White);
                PlanLayout(width, height, s,g);

            }

            // 保存
            this.statusLabel.Text = "保存しています...";
            this.progressBar1.Value = data.Length;
            Application.DoEvents();

            // ファイル名生成
            string fontinfo = data.Length+"件-"+currentFont.Name + "-" + currentFont.Size + "pt." + currentFont.Style.ToString();
            bmp.Save(fontinfo+".png", System.Drawing.Imaging.ImageFormat.Png);

            this.progressBar1.Value = 0;
            this.statusLabel.Text = "完了しました";
            this.button2.Text = "画像生成";
            isRunning = false;
        }


        /// <summary>
        /// 配置のための計算を行います
        /// </summary>
        /// <param name="width"></param>s
        /// <param name="height"></param>
        /// <param name="step"></param>
        /// <returns></returns>
        private int PlanLayout(int width, int height, int step, Graphics g)
        {
            int result = defaultXStep;

            int y = 0;
            int x = 0;
            int xmax = width;
            int count = 0;
            this.progressBar1.Minimum = 0;
            this.progressBar1.Maximum = data.Length;
            for (int i = 1; i < data.Length && y < height && isRunning; i++)
            {
                this.progressBar1.Value = i;
                Application.DoEvents();

                // 1行分の配置計画を行う
                List<string> str = new List<string>();
                List<int> strWidth = new List<int>();
                List<int> strX = new List<int>();
                int ii = i;
                int xx = x;
                int c = 0;
                do
                {
                    // 文字列の左側の位置を保存
                    strX.Add(xx);

                    // 表示文字列を生成
                    string s = _str[ii];
                    str.Add(s);

                    // 文字幅を計測
                    int stringSize = _strWidth[ii];
                    strWidth.Add(1 + stringSize);
                    xx += step + 1 + stringSize;

                    ii++;
                    c++;
                } while (xx < xmax && ii < data.Length);

                // データの最後でない場合は最後に追加した文字列を無視
                if (ii < data.Length)
                {
                    c--;
                }

                y = (height-stringHeight) * count / (lines-1);
                if (c == 0)
                {
                    if (g != null)
                    {
                        rt.RenderString(str[0], currentFont, g, new PointF(0, y));
                    }
                    result = 0;
                }
                else
                {
                    // 右端にぴったり寄せるように文字位置を再配置
                    strWidth[c - 1] = _strWidth[i + c - 1];
                    int last = xmax - (strX[c - 1] + strWidth[c - 1]) - 3;
                    if (last < 0) last = 0;
                    result = last;
                    int[] strSpace = new int[c - 1];
                    for (int j = 0; j < strSpace.Length; j++)
                    {
                        strSpace[j] = step;
                    }
                    while (last > 0 && strSpace.Length > 0)
                    {
                        strSpace[last % strSpace.Length]++;
                        last--;
                    }

                    // 文字列を描画
                    int px = 0;
                    if (g != null)
                    {
                        for (int j = 0; j < c - 1; j++)
                        {
                            rt.RenderString(str[j], currentFont, g, new PointF(px, y));
                            px += strWidth[j] + strSpace[j];
                        }
                        rt.RenderString(str[c - 1], currentFont, g, new PointF(px, y));
                    }
                    else
                    {
                        for (int j = 0; j < c - 1; j++)
                        {
                            px += strWidth[j] + strSpace[j];
                        }
                    }
                    i += c - 1;
                }
                x = 0;
                count++;
            }
            if (y > height)
            {
                result = -1;
            }
            lines = count;
            this.progressBar1.Value = 0;
            return result;

        }



        /// <summary>
        /// 全ての文字幅を取得します
        /// </summary>
        /// <returns>各文字幅(ピクセル)</returns>
        private void GetAllTextSize(string[] messages, Font font)
        {
            _str.Clear();
            _strWidth.Clear();
            stringHeight = 0;

            this.progressBar1.Minimum = 0;
            this.progressBar1.Maximum = messages.Length;

            for(int i=0; i < data.Length; i++){
                this.progressBar1.Value = i;
                Application.DoEvents();

                // 文字列の追加
                string s = ExpandMessage(i,data[i]);
                _str.Add(s);

                // 文字幅の追加
                Rectangle rect = rt.MeasureString(s, font);
                int size = rect.Width;

                // 文字列の高さの更新
                if (stringHeight < rect.Height) stringHeight = rect.Height;
                _strWidth.Add(size);
            }
        }

        // メッセージ展開処理
        string ExpandMessage(int id, string csvText)
        {
            int id2 = id + this.offsetCount - 1;

            // 表示文字列を生成
            string s = csvText;
            int pos = s.IndexOf(',');
            string ss = s.Substring(pos + 1).Trim();
            int pos2 = ss.IndexOf(',');

            string result = "";
            if (pos2>0)
            {
                string user = ss.Substring(0, pos2).Trim();
                string message = ss.Substring(pos2 + 1).Trim();
                result = this.messageFormat.Replace("$num",id2.ToString());
                result = result.Replace("$user",user);
                result = result.Replace("$message",message);
            }else{
                string user = ss;
                result = this.messageFormatNoMessage.Replace("$num",id2.ToString());
                result = result.Replace("$user",user);
            }

            return result;
        }

        // サンプル表示処理
        private void DrawSampleText(int width, int height, int step, Bitmap bmp)
        {
            if (data == null) return;
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.White);
                int id = 1;
                int x = 0;
                int y = 0;
                while (id < data.Length && y < height)
                {
                    string message = ExpandMessage(id, data[id]);
                    Rectangle rect = rt.RenderString(message, currentFont, g, new PointF(x, y));
                    x += rect.Width + defaultXStep + 2;
                    if (x > width)
                    {
                        x = 0;
                        y += defaultYStep;
                    }
                    id++;
                }

            }
        }

        private void RedrawSample()
        {
            // フォント情報を表示
            string fontinfo = currentFont.Name + ", " + currentFont.Size + " pt, " + currentFont.Style.ToString();
            this.labelFont.Text = fontinfo;

            // サンプル表示用ウィンドウに描画
            if (this.pictureBox1.Width > 0)
            {
                Bitmap bmp = new Bitmap(this.pictureBox1.Width, this.pictureBox1.Height);
                isRunning = true;
                DrawSampleText(bmp.Width, bmp.Height, defaultXStep, bmp);
                isRunning = false;
                this.pictureBox1.Image = bmp;
            }
        }

        // サンプル更新イベント処理
        private void button1_Click(object sender, EventArgs e)
        {
            if (fontDialog1.ShowDialog() == DialogResult.OK)
            {
                currentFont = fontDialog1.Font;
            }
            RedrawSample();

        }

        private void pictureBox1_SizeChanged(object sender, EventArgs e)
        {
            RedrawSample();
        }

        private void textBoxNoMessage_TextChanged(object sender, EventArgs e)
        {
            if (textBoxMessage.Text.Trim().Length > 0)
            {
                this.messageFormatNoMessage = textBoxNoMessage.Text;
            }
            else
            {
                textBoxNoMessage.Text = this.messageFormatNoMessage;
            }
            RedrawSample();
        }

        private void textBoxMessage_TextChanged(object sender, EventArgs e)
        {
            if (textBoxMessage.Text.Trim().Length > 0)
            {
                this.messageFormat = textBoxMessage.Text;
            }
            else
            {
                textBoxMessage.Text = this.messageFormat;
            }
            RedrawSample();
        }

        private void textBoxOffset_TextChanged(object sender, EventArgs e)
        {
            int a = 0;
            try
            {
                a = Convert.ToInt32(this.textBoxOffset.Text);
            }
            catch
            {
                a = this.offsetCount;
            }
            finally
            {
                this.offsetCount = a;
                this.textBoxOffset.Text = a.ToString();
                RedrawSample();
            }
        }
    }
}
