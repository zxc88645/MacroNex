namespace MacroNex.Domain.ValueObjects;

/// <summary>
/// 滑鼠校準數據，用於 HID ΔXY 到實際像素的轉換。
/// 支援非線性模型（考慮 Windows 滑鼠加速）。
/// </summary>
public class MouseCalibrationData
{
    /// <summary>
    /// 校準時間
    /// </summary>
    public DateTime CalibratedAt { get; set; }

    /// <summary>
    /// X 軸校準樣本點 (HID Delta -> Actual Pixel)
    /// </summary>
    public List<CalibrationPoint> PointsX { get; set; } = new();

    /// <summary>
    /// Y 軸校準樣本點 (HID Delta -> Actual Pixel)
    /// </summary>
    public List<CalibrationPoint> PointsY { get; set; } = new();

    /// <summary>
    /// 根據目標像素移動量，計算需要發送的 HID delta（反向查找）。
    /// 使用線性插值在校準點之間進行計算。
    /// </summary>
    /// <param name="targetPixelDelta">目標像素移動量</param>
    /// <param name="useYAxis">是否使用 Y 軸數據</param>
    /// <returns>需要發送的 HID delta 值</returns>
    public int CalculateHidDelta(double targetPixelDelta, bool useYAxis = false)
    {
        var points = useYAxis ? PointsY : PointsX;
        
        if (points.Count == 0)
            return (int)Math.Round(targetPixelDelta); // 無校準數據，假設 1:1

        // 處理負值：取絕對值計算後再恢復符號
        bool isNegative = targetPixelDelta < 0;
        double absTarget = Math.Abs(targetPixelDelta);

        // 排序確保點按 ActualPixelDelta 升序排列
        var sortedPoints = points
            .Where(p => p.ActualPixelDelta >= 0)
            .OrderBy(p => p.ActualPixelDelta)
            .ToList();

        if (sortedPoints.Count == 0)
            return (int)Math.Round(targetPixelDelta);

        // 邊界處理
        if (absTarget <= sortedPoints[0].ActualPixelDelta)
        {
            // 小於最小校準點，使用第一個點的比例
            double ratio = sortedPoints[0].HidDelta / Math.Max(sortedPoints[0].ActualPixelDelta, 0.001);
            int result = (int)Math.Round(absTarget * ratio);
            return isNegative ? -result : result;
        }

        if (absTarget >= sortedPoints[^1].ActualPixelDelta)
        {
            // 大於最大校準點，使用最後一個點的比例
            double ratio = sortedPoints[^1].HidDelta / Math.Max(sortedPoints[^1].ActualPixelDelta, 0.001);
            int result = (int)Math.Round(absTarget * ratio);
            return isNegative ? -result : result;
        }

        // 線性插值
        for (int i = 0; i < sortedPoints.Count - 1; i++)
        {
            var p1 = sortedPoints[i];
            var p2 = sortedPoints[i + 1];

            if (absTarget >= p1.ActualPixelDelta && absTarget <= p2.ActualPixelDelta)
            {
                // 在 p1 和 p2 之間進行插值
                double t = (absTarget - p1.ActualPixelDelta) / 
                           Math.Max(p2.ActualPixelDelta - p1.ActualPixelDelta, 0.001);
                double hidDelta = p1.HidDelta + t * (p2.HidDelta - p1.HidDelta);
                int result = (int)Math.Round(hidDelta);
                return isNegative ? -result : result;
            }
        }

        return (int)Math.Round(targetPixelDelta);
    }

    /// <summary>
    /// 根據 HID delta 計算預期的實際像素移動量（正向查找）。
    /// 用於預覽校準效果。
    /// </summary>
    /// <param name="hidDelta">HID delta 值</param>
    /// <param name="useYAxis">是否使用 Y 軸數據</param>
    /// <returns>預期的像素移動量</returns>
    public double CalculatePixelDelta(int hidDelta, bool useYAxis = false)
    {
        var points = useYAxis ? PointsY : PointsX;
        
        if (points.Count == 0)
            return hidDelta; // 無校準數據，假設 1:1

        // 處理負值
        bool isNegative = hidDelta < 0;
        int absHid = Math.Abs(hidDelta);

        // 排序確保點按 HidDelta 升序排列
        var sortedPoints = points
            .Where(p => p.HidDelta >= 0)
            .OrderBy(p => p.HidDelta)
            .ToList();

        if (sortedPoints.Count == 0)
            return hidDelta;

        // 邊界處理
        if (absHid <= sortedPoints[0].HidDelta)
        {
            double ratio = sortedPoints[0].ActualPixelDelta / Math.Max(sortedPoints[0].HidDelta, 0.001);
            double result = absHid * ratio;
            return isNegative ? -result : result;
        }

        if (absHid >= sortedPoints[^1].HidDelta)
        {
            double ratio = sortedPoints[^1].ActualPixelDelta / Math.Max(sortedPoints[^1].HidDelta, 0.001);
            double result = absHid * ratio;
            return isNegative ? -result : result;
        }

        // 線性插值
        for (int i = 0; i < sortedPoints.Count - 1; i++)
        {
            var p1 = sortedPoints[i];
            var p2 = sortedPoints[i + 1];

            if (absHid >= p1.HidDelta && absHid <= p2.HidDelta)
            {
                double t = (absHid - p1.HidDelta) / Math.Max(p2.HidDelta - p1.HidDelta, 0.001);
                double pixelDelta = p1.ActualPixelDelta + t * (p2.ActualPixelDelta - p1.ActualPixelDelta);
                return isNegative ? -pixelDelta : pixelDelta;
            }
        }

        return hidDelta;
    }

    /// <summary>
    /// 檢查校準數據是否有效
    /// </summary>
    public bool IsValid => PointsX.Count >= 2 || PointsY.Count >= 2;

    /// <summary>
    /// 取得校準摘要資訊
    /// </summary>
    public string GetSummary()
    {
        if (!IsValid)
            return "無有效校準數據";

        var avgRatioX = PointsX.Count > 0
            ? PointsX.Where(p => p.HidDelta != 0).Average(p => p.ActualPixelDelta / p.HidDelta)
            : 0;
        var avgRatioY = PointsY.Count > 0
            ? PointsY.Where(p => p.HidDelta != 0).Average(p => p.ActualPixelDelta / p.HidDelta)
            : 0;

        return $"校準於 {CalibratedAt:yyyy-MM-dd HH:mm}, X 平均比例: {avgRatioX:F3}, Y 平均比例: {avgRatioY:F3}";
    }
}

/// <summary>
/// 單個校準點，記錄 HID Delta 和對應的實際像素移動量
/// </summary>
public class CalibrationPoint
{
    /// <summary>
    /// 發送的 HID Delta 值
    /// </summary>
    public int HidDelta { get; set; }

    /// <summary>
    /// 實際測量的像素移動量
    /// </summary>
    public double ActualPixelDelta { get; set; }

    /// <summary>
    /// 計算比例 (ActualPixel / HidDelta)
    /// </summary>
    public double Ratio => HidDelta != 0 ? ActualPixelDelta / HidDelta : 0;

    public override string ToString() => $"HID: {HidDelta} -> Pixel: {ActualPixelDelta:F1} (Ratio: {Ratio:F3})";
}
