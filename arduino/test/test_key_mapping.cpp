/*
 * Key Mapping Tests
 * Tests for HID to VirtualKey and VirtualKey to Arduino key mappings
 */

#include <unity.h>

// Simplified key mapping functions for testing
uint16_t mapHidKeyToVk(uint8_t hidKey, uint8_t modifiers) {
  // Letters A-Z (HID: 4-29, VK: 0x41-0x5A)
  if (hidKey >= 4 && hidKey <= 29) {
    return 0x41 + (hidKey - 4);
  }
  
  // Numbers 1-9, 0 (HID: 30-39, VK: 0x31-0x39, 0x30)
  if (hidKey >= 30 && hidKey <= 39) {
    if (hidKey == 39) return 0x30; // 0
    return 0x31 + (hidKey - 30); // 1-9
  }
  
  // Special keys mapping
  switch (hidKey) {
    case 40: return 0x0D; // Enter -> VK_RETURN
    case 41: return 0x1B; // Escape -> VK_ESCAPE
    case 42: return 0x08; // Backspace -> VK_BACK
    case 43: return 0x09; // Tab -> VK_TAB
    case 44: return 0x20; // Space -> VK_SPACE
    
    // Function keys
    case 58: return 0x70; // F1 -> VK_F1
    case 59: return 0x71; // F2 -> VK_F2
    case 60: return 0x72; // F3 -> VK_F3
    case 61: return 0x73; // F4 -> VK_F4
    case 62: return 0x74; // F5 -> VK_F5
    case 63: return 0x75; // F6 -> VK_F6
    case 64: return 0x76; // F7 -> VK_F7
    case 65: return 0x77; // F8 -> VK_F8
    case 66: return 0x78; // F9 -> VK_F9
    case 67: return 0x79; // F10 -> VK_F10
    case 68: return 0x7A; // F11 -> VK_F11
    case 69: return 0x7B; // F12 -> VK_F12
    
    // Navigation keys
    case 82: return 0x26; // Up Arrow -> VK_UP
    case 83: return 0x25; // Left Arrow -> VK_LEFT
    case 84: return 0x28; // Down Arrow -> VK_DOWN
    case 85: return 0x27; // Right Arrow -> VK_RIGHT
    
    default:
      return 0; // Unsupported key
  }
}

uint8_t mapVkToArduinoKey(uint16_t vk) {
  // Letters A-Z
  if (vk >= 0x41 && vk <= 0x5A) {
    return (vk - 0x41) + 'a';
  }
  
  // Numbers 0-9
  if (vk >= 0x30 && vk <= 0x39) {
    return (vk - 0x30) + '0';
  }
  
  // Special keys (simplified for testing)
  switch (vk) {
    case 0x08: return 0xB2; // KEY_BACKSPACE
    case 0x09: return 0xB3; // KEY_TAB
    case 0x0D: return 0xB0; // KEY_RETURN
    case 0x1B: return 0xB1; // KEY_ESC
    case 0x20: return ' ';  // Space
    case 0x25: return 0xD8; // KEY_LEFT_ARROW
    case 0x26: return 0xDA; // KEY_UP_ARROW
    case 0x27: return 0xD7; // KEY_RIGHT_ARROW
    case 0x28: return 0xD9; // KEY_DOWN_ARROW
    
    // Function keys
    case 0x70: return 0xC2; // KEY_F1
    case 0x71: return 0xC3; // KEY_F2
    case 0x72: return 0xC4; // KEY_F3
    case 0x73: return 0xC5; // KEY_F4
    case 0x74: return 0xC6; // KEY_F5
    case 0x75: return 0xC7; // KEY_F6
    case 0x76: return 0xC8; // KEY_F7
    case 0x77: return 0xC9; // KEY_F8
    case 0x78: return 0xCA; // KEY_F9
    case 0x79: return 0xCB; // KEY_F10
    case 0x7A: return 0xCC; // KEY_F11
    case 0x7B: return 0xCD; // KEY_F12
    
    default:
      return 0; // Unsupported key
  }
}

void setUp(void) {
  // Set up test fixtures
}

void tearDown(void) {
  // Clean up after tests
}

void test_hid_to_vk_letters() {
  // Test A-Z mapping
  TEST_ASSERT_EQUAL(0x41, mapHidKeyToVk(4, 0));   // A
  TEST_ASSERT_EQUAL(0x42, mapHidKeyToVk(5, 0));   // B
  TEST_ASSERT_EQUAL(0x5A, mapHidKeyToVk(29, 0));  // Z
}

void test_hid_to_vk_numbers() {
  // Test 1-9, 0 mapping
  TEST_ASSERT_EQUAL(0x31, mapHidKeyToVk(30, 0));  // 1
  TEST_ASSERT_EQUAL(0x32, mapHidKeyToVk(31, 0));  // 2
  TEST_ASSERT_EQUAL(0x39, mapHidKeyToVk(38, 0));  // 9
  TEST_ASSERT_EQUAL(0x30, mapHidKeyToVk(39, 0));  // 0
}

void test_hid_to_vk_special_keys() {
  TEST_ASSERT_EQUAL(0x0D, mapHidKeyToVk(40, 0));  // Enter
  TEST_ASSERT_EQUAL(0x1B, mapHidKeyToVk(41, 0));  // Escape
  TEST_ASSERT_EQUAL(0x08, mapHidKeyToVk(42, 0));  // Backspace
  TEST_ASSERT_EQUAL(0x09, mapHidKeyToVk(43, 0));  // Tab
  TEST_ASSERT_EQUAL(0x20, mapHidKeyToVk(44, 0));  // Space
}

void test_hid_to_vk_function_keys() {
  TEST_ASSERT_EQUAL(0x70, mapHidKeyToVk(58, 0));  // F1
  TEST_ASSERT_EQUAL(0x71, mapHidKeyToVk(59, 0));  // F2
  TEST_ASSERT_EQUAL(0x7B, mapHidKeyToVk(69, 0));  // F12
}

void test_hid_to_vk_arrow_keys() {
  TEST_ASSERT_EQUAL(0x26, mapHidKeyToVk(82, 0));  // Up
  TEST_ASSERT_EQUAL(0x25, mapHidKeyToVk(83, 0));  // Left
  TEST_ASSERT_EQUAL(0x28, mapHidKeyToVk(84, 0));  // Down
  TEST_ASSERT_EQUAL(0x27, mapHidKeyToVk(85, 0));  // Right
}

void test_hid_to_vk_unsupported() {
  TEST_ASSERT_EQUAL(0, mapHidKeyToVk(0, 0));      // Unsupported
  TEST_ASSERT_EQUAL(0, mapHidKeyToVk(200, 0));     // Unsupported
}

void test_vk_to_arduino_letters() {
  // Test A-Z mapping
  TEST_ASSERT_EQUAL('a', mapVkToArduinoKey(0x41));  // A -> a
  TEST_ASSERT_EQUAL('b', mapVkToArduinoKey(0x42));  // B -> b
  TEST_ASSERT_EQUAL('z', mapVkToArduinoKey(0x5A));  // Z -> z
}

void test_vk_to_arduino_numbers() {
  // Test 0-9 mapping
  TEST_ASSERT_EQUAL('0', mapVkToArduinoKey(0x30));  // 0
  TEST_ASSERT_EQUAL('1', mapVkToArduinoKey(0x31));  // 1
  TEST_ASSERT_EQUAL('9', mapVkToArduinoKey(0x39));  // 9
}

void test_vk_to_arduino_special_keys() {
  TEST_ASSERT_EQUAL(0xB2, mapVkToArduinoKey(0x08));  // Backspace
  TEST_ASSERT_EQUAL(0xB3, mapVkToArduinoKey(0x09));  // Tab
  TEST_ASSERT_EQUAL(0xB0, mapVkToArduinoKey(0x0D));  // Return
  TEST_ASSERT_EQUAL(0xB1, mapVkToArduinoKey(0x1B));  // Escape
  TEST_ASSERT_EQUAL(' ', mapVkToArduinoKey(0x20));   // Space
}

void test_vk_to_arduino_function_keys() {
  TEST_ASSERT_EQUAL(0xC2, mapVkToArduinoKey(0x70));  // F1
  TEST_ASSERT_EQUAL(0xC3, mapVkToArduinoKey(0x71));  // F2
  TEST_ASSERT_EQUAL(0xCD, mapVkToArduinoKey(0x7B));  // F12
}

void test_vk_to_arduino_arrow_keys() {
  TEST_ASSERT_EQUAL(0xDA, mapVkToArduinoKey(0x26));  // Up
  TEST_ASSERT_EQUAL(0xD8, mapVkToArduinoKey(0x25));  // Left
  TEST_ASSERT_EQUAL(0xD9, mapVkToArduinoKey(0x28));  // Down
  TEST_ASSERT_EQUAL(0xD7, mapVkToArduinoKey(0x27));  // Right
}

void test_vk_to_arduino_unsupported() {
  TEST_ASSERT_EQUAL(0, mapVkToArduinoKey(0x00));      // Unsupported
  TEST_ASSERT_EQUAL(0, mapVkToArduinoKey(0xFFFF));   // Unsupported
}

void test_round_trip_mapping() {
  // Test that HID -> VK -> Arduino key mapping works for common keys
  uint8_t hidKey = 4;  // A
  uint16_t vk = mapHidKeyToVk(hidKey, 0);
  uint8_t arduinoKey = mapVkToArduinoKey(vk);
  TEST_ASSERT_EQUAL('a', arduinoKey);
  
  hidKey = 30;  // 1
  vk = mapHidKeyToVk(hidKey, 0);
  arduinoKey = mapVkToArduinoKey(vk);
  TEST_ASSERT_EQUAL('1', arduinoKey);
}

int main(void) {
  UNITY_BEGIN();
  
  RUN_TEST(test_hid_to_vk_letters);
  RUN_TEST(test_hid_to_vk_numbers);
  RUN_TEST(test_hid_to_vk_special_keys);
  RUN_TEST(test_hid_to_vk_function_keys);
  RUN_TEST(test_hid_to_vk_arrow_keys);
  RUN_TEST(test_hid_to_vk_unsupported);
  RUN_TEST(test_vk_to_arduino_letters);
  RUN_TEST(test_vk_to_arduino_numbers);
  RUN_TEST(test_vk_to_arduino_special_keys);
  RUN_TEST(test_vk_to_arduino_function_keys);
  RUN_TEST(test_vk_to_arduino_arrow_keys);
  RUN_TEST(test_vk_to_arduino_unsupported);
  RUN_TEST(test_round_trip_mapping);
  
  return UNITY_END();
}
