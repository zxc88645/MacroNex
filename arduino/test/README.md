# Arduino 固件測試

本目錄包含 Arduino 固件的單元測試，使用 PlatformIO 的 Unity 測試框架。

## 測試結構

- `test_protocol.cpp` - 協議編碼/解碼和校驗和測試
- `test_key_mapping.cpp` - HID 到 VirtualKey 和 VirtualKey 到 Arduino 鍵映射測試
- `test_command_parsing.cpp` - 命令解析和驗證測試

## 運行測試

### 使用 PlatformIO CLI

```bash
cd arduino
pio test -e native
```

### 運行特定測試

```bash
# 運行協議測試
pio test -e native -f test_protocol

# 運行鍵映射測試
pio test -e native -f test_key_mapping

# 運行命令解析測試
pio test -e native -f test_command_parsing
```

### 查看詳細輸出

```bash
pio test -e native -v
```

## 測試覆蓋範圍

### 協議測試 (`test_protocol.cpp`)
- ✅ 校驗和計算
- ✅ 命令編碼（滑鼠移動、鍵盤輸入、錄製控制等）
- ✅ 事件編碼（滑鼠移動、點擊、鍵盤輸入等）
- ✅ 時間戳處理

### 鍵映射測試 (`test_key_mapping.cpp`)
- ✅ HID 鍵碼到 VirtualKey 映射
  - 字母 (A-Z)
  - 數字 (0-9)
  - 特殊鍵（Enter、Escape、Backspace 等）
  - 功能鍵 (F1-F12)
  - 方向鍵
- ✅ VirtualKey 到 Arduino 鍵碼映射
  - 字母和數字
  - 特殊鍵
  - 功能鍵
  - 方向鍵
- ✅ 往返映射測試

### 命令解析測試 (`test_command_parsing.cpp`)
- ✅ 命令緩衝區重置
- ✅ 命令類型驗證
- ✅ 命令解析狀態機
- ✅ 數據長度驗證
- ✅ 無效命令處理

## 添加新測試

1. 創建新的測試文件 `test_<feature>.cpp`
2. 包含 Unity 測試框架：`#include <unity.h>`
3. 實現 `setUp()` 和 `tearDown()` 函數（可選）
4. 實現測試函數，使用 `TEST_ASSERT_*` 宏
5. 在 `main()` 函數中註冊測試：`RUN_TEST(test_function_name)`

示例：

```cpp
#include <unity.h>

void setUp(void) {
  // 設置測試環境
}

void tearDown(void) {
  // 清理測試環境
}

void test_example() {
  TEST_ASSERT_EQUAL(1, 1);
}

int main(void) {
  UNITY_BEGIN();
  RUN_TEST(test_example);
  return UNITY_END();
}
```

## 注意事項

- 測試在 `native` 環境中運行，不需要實際的 Arduino 硬件
- 某些硬件相關的功能（如 USB Host Shield）無法在 native 環境中測試
- 對於硬件相關的測試，建議使用集成測試或手動測試
