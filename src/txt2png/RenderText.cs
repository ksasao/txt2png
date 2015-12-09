using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;

namespace txt2png
{
    public class RenderText
    {
        static readonly byte[] circle = { 0, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 131 };

        /// <summary>
        /// 指定した位置に文字列を描画します。Windows Vista 以降でも
        /// タイ語を利用したAA(眉毛)がある程度正しく表示されるようにします。
        /// </summary>
        /// <param name="text">描画対象文字列</param>
        /// <param name="font">フォント</param>
        /// <param name="g">グラフィックコンテキスト</param>
        /// <param name="point">表示位置の左上</param>
        /// <returns>描画領域を表す長方形</returns>
        public Rectangle RenderString(string text, Font font, Graphics g, PointF point)
        {
            return RenderMain(text, font, g, point, true);
        }

        public Rectangle MeasureString(string text, Font font)
        {
            Rectangle rect = new Rectangle();
            Bitmap bmp = new Bitmap(1, 1);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                rect = RenderMain(text, font, g, new Point(0, 0), false);
            }
            bmp.Dispose();
            return rect;
        }

        private Rectangle RenderMain(string text, Font font, Graphics g, PointF point, bool isRender)
        {
            int minX0 = int.MaxValue;
            int maxX0 = int.MinValue;
            int minY0 = int.MaxValue;
            int maxY0 = int.MinValue;

            float next = point.X;
            for (int u = 0; u < text.Length; u++)
            {
                // 文字列から1文字ずつ切り出して処理
                string s = text[u].ToString();

                GraphicsPath gPath = new System.Drawing.Drawing2D.GraphicsPath();
                gPath.AddString(s, font.FontFamily, (int)font.Style,
                    font.Size, new PointF(next, point.Y), StringFormat.GenericTypographic);

                if (gPath.PathData.Points.Length >= circle.Length * 16)
                {
                    bool flag = true;
                    // 組み合わせ文字かどうかの判定(一部の組み合わせ文字のみ対応)
                    for (int i = 0; i < circle.Length * 16; i++)
                    {
                        if (circle[i % circle.Length] != gPath.PathTypes[i])
                        {
                            flag = false;
                            break;
                        }
                    }

                    if (flag)
                    {
                        // 組み合わせ部分の○をマスク
                        int minX = int.MaxValue;
                        int maxX = int.MinValue;
                        int minY = int.MaxValue;
                        int maxY = int.MinValue;

                        // gPath は GDI+ にアクセスするため非常に遅い
                        // したがってループ内で gPath のプロパティを呼ばないこと
                        PointF[] p = gPath.PathPoints;
                        for (int i = 0; i < 16; i++)
                        {
                            for (int j = 0; j < circle.Length; j++)
                            {
                                int x = (int)p[i * 13 + j].X;
                                int y = (int)p[i * 13 + j].Y;
                                if (x < minX) minX = x;
                                if (maxX < x) maxX = x;
                                if (y < minY) minY = y;
                                if (maxY < y) maxY = y;
                            }
                        }
                        // アンチエイリアス処理が有効な場合に消し損ねることがある場合に対応
                        int b = 1;
                        g.ExcludeClip(new Rectangle(minX - (int)gPath.GetBounds().Width - b, minY - b + 1, maxX - minX + b * 2, maxY - minY + b * 2));
                        Matrix mat = new Matrix();
                        mat.Translate(-gPath.GetBounds().Width, 0);
                        gPath.Transform(mat);
                    }
                }

                // 文字列の描画領域を更新
                RectangleF rect = gPath.GetBounds();
                int x0 = (int)rect.Left;
                int x1 = (int)rect.Right;
                int y0 = (int)rect.Top;
                int y1 = (int)rect.Bottom;

                // 次の座標へ
                if (x1 > next)
                {
                    if (x0 < minX0) minX0 = x0;
                    if (maxX0 < x1) maxX0 = x1;
                    if (y0 < minY0) minY0 = y0;
                    if (maxY0 < y1) maxY0 = y1;
                    next = x1 + 1;
                }
                else
                {
                    next += (int) font.Size / 3;
                }
                // フラグが立っている場合のみ描画
                if (isRender)
                {
                    g.FillPath(Brushes.Black, gPath);
                    g.ResetClip();
                    gPath.Dispose();
                }
            }
            return new Rectangle(minX0, minY0, maxX0 - minX0 + 1, maxY0 - minY0 + 1);
        }

    }
}
