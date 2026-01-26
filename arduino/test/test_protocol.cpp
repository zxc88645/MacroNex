/*
 * Protocol Tests
 * Tests for command/event encoding, decoding, and checksum calculation
 */

#include <unity.h>
#include <string.h>

// Protocol constants (from firmware)
const byte CMD_MOUSE_MOVE_ABS = 0x01;
const byte CMD_MOUSE_MOVE_REL = 0x02;
const byte CMD_MOUSE_CLICK = 0x03;
const byte CMD_KEYBOARD_TEXT = 0x04;
const byte CMD_KEY_PRESS = 0x05;
const byte CMD_DELAY = 0x06;
const byte CMD_START_RECORDING = 0x10;
const byte CMD_STOP_RECORDING = 0x11;
const byte CMD_STATUS_QUERY = 0x20;
const byte CMD_ERROR = 0xFF;

const byte EVT_MOUSE_MOVE = 0x01;
const byte EVT_MOUSE_CLICK = 0x02;
const byte EVT_KEYBOARD_INPUT = 0x03;
const byte EVT_STATUS_RESPONSE = 0x20;
const byte EVT_ERROR = 0xFF;

// Checksum calculation (from firmware)
byte calculateChecksum(byte* data, int length) {
  byte checksum = 0;
  for (int i = 0; i < length; i++) {
    checksum ^= data[i];
  }
  return checksum;
}

// Helper function to create a command packet
void createCommand(byte cmdType, byte* data, uint16_t dataLength, byte* output, int* outputLength) {
  int pos = 0;
  output[pos++] = cmdType;
  output[pos++] = (byte)(dataLength & 0xFF);
  output[pos++] = (byte)((dataLength >> 8) & 0xFF);
  
  for (uint16_t i = 0; i < dataLength; i++) {
    output[pos++] = data[i];
  }
  
  byte checksum = calculateChecksum(output, pos);
  output[pos++] = checksum;
  
  *outputLength = pos;
}

// Helper function to create an event packet
void createEvent(byte eventType, byte* data, uint16_t dataLength, uint32_t timestamp, byte* output, int* outputLength) {
  int pos = 0;
  output[pos++] = eventType;
  output[pos++] = (byte)(dataLength & 0xFF);
  output[pos++] = (byte)((dataLength >> 8) & 0xFF);
  
  for (uint16_t i = 0; i < dataLength; i++) {
    output[pos++] = data[i];
  }
  
  output[pos++] = (byte)(timestamp & 0xFF);
  output[pos++] = (byte)((timestamp >> 8) & 0xFF);
  output[pos++] = (byte)((timestamp >> 16) & 0xFF);
  output[pos++] = (byte)((timestamp >> 24) & 0xFF);
  
  byte checksum = calculateChecksum(output, pos);
  output[pos++] = checksum;
  
  *outputLength = pos;
}

void setUp(void) {
  // Set up test fixtures
}

void tearDown(void) {
  // Clean up after tests
}

void test_checksum_calculation() {
  byte data1[] = {0x01, 0x02, 0x03};
  byte checksum1 = calculateChecksum(data1, 3);
  TEST_ASSERT_EQUAL(0x00, checksum1); // 0x01 ^ 0x02 ^ 0x03 = 0x00
  
  byte data2[] = {0xFF, 0x00, 0xFF};
  byte checksum2 = calculateChecksum(data2, 3);
  TEST_ASSERT_EQUAL(0x00, checksum2); // 0xFF ^ 0x00 ^ 0xFF = 0x00
  
  byte data3[] = {0x12, 0x34, 0x56, 0x78};
  byte checksum3 = calculateChecksum(data3, 4);
  TEST_ASSERT_EQUAL(0x5C, checksum3); // 0x12 ^ 0x34 ^ 0x56 ^ 0x78 = 0x5C
}

void test_command_encoding_mouse_move_abs() {
  byte data[4] = {0x00, 0x64, 0x00, 0xC8}; // x=100, y=200
  byte output[256];
  int outputLength;
  
  createCommand(CMD_MOUSE_MOVE_ABS, data, 4, output, &outputLength);
  
  TEST_ASSERT_EQUAL(8, outputLength); // Type(1) + Length(2) + Data(4) + Checksum(1)
  TEST_ASSERT_EQUAL(CMD_MOUSE_MOVE_ABS, output[0]);
  TEST_ASSERT_EQUAL(4, output[1] | (output[2] << 8));
  TEST_ASSERT_EQUAL(data[0], output[3]);
  TEST_ASSERT_EQUAL(data[1], output[4]);
  TEST_ASSERT_EQUAL(data[2], output[5]);
  TEST_ASSERT_EQUAL(data[3], output[6]);
  
  // Verify checksum
  byte expectedChecksum = calculateChecksum(output, outputLength - 1);
  TEST_ASSERT_EQUAL(expectedChecksum, output[outputLength - 1]);
}

void test_command_encoding_keyboard_text() {
  byte data[] = {'H', 'e', 'l', 'l', 'o'};
  byte output[256];
  int outputLength;
  
  createCommand(CMD_KEYBOARD_TEXT, data, 5, output, &outputLength);
  
  TEST_ASSERT_EQUAL(9, outputLength); // Type(1) + Length(2) + Data(5) + Checksum(1)
  TEST_ASSERT_EQUAL(CMD_KEYBOARD_TEXT, output[0]);
  TEST_ASSERT_EQUAL(5, output[1] | (output[2] << 8));
  
  // Verify checksum
  byte expectedChecksum = calculateChecksum(output, outputLength - 1);
  TEST_ASSERT_EQUAL(expectedChecksum, output[outputLength - 1]);
}

void test_event_encoding_mouse_move() {
  byte data[4] = {0x00, 0x64, 0x00, 0xC8}; // x=100, y=200
  uint32_t timestamp = 12345;
  byte output[256];
  int outputLength;
  
  createEvent(EVT_MOUSE_MOVE, data, 4, timestamp, output, &outputLength);
  
  TEST_ASSERT_EQUAL(12, outputLength); // Type(1) + Length(2) + Data(4) + Timestamp(4) + Checksum(1)
  TEST_ASSERT_EQUAL(EVT_MOUSE_MOVE, output[0]);
  TEST_ASSERT_EQUAL(4, output[1] | (output[2] << 8));
  
  // Verify timestamp
  uint32_t receivedTimestamp = output[7] | (output[8] << 8) | (output[9] << 16) | (output[10] << 24);
  TEST_ASSERT_EQUAL(timestamp, receivedTimestamp);
  
  // Verify checksum
  byte expectedChecksum = calculateChecksum(output, outputLength - 1);
  TEST_ASSERT_EQUAL(expectedChecksum, output[outputLength - 1]);
}

void test_command_encoding_start_recording() {
  byte output[256];
  int outputLength;
  
  createCommand(CMD_START_RECORDING, NULL, 0, output, &outputLength);
  
  TEST_ASSERT_EQUAL(4, outputLength); // Type(1) + Length(2) + Checksum(1)
  TEST_ASSERT_EQUAL(CMD_START_RECORDING, output[0]);
  TEST_ASSERT_EQUAL(0, output[1] | (output[2] << 8));
  
  // Verify checksum
  byte expectedChecksum = calculateChecksum(output, outputLength - 1);
  TEST_ASSERT_EQUAL(expectedChecksum, output[outputLength - 1]);
}

void test_command_encoding_status_query() {
  byte output[256];
  int outputLength;
  
  createCommand(CMD_STATUS_QUERY, NULL, 0, output, &outputLength);
  
  TEST_ASSERT_EQUAL(4, outputLength);
  TEST_ASSERT_EQUAL(CMD_STATUS_QUERY, output[0]);
  
  // Verify checksum
  byte expectedChecksum = calculateChecksum(output, outputLength - 1);
  TEST_ASSERT_EQUAL(expectedChecksum, output[outputLength - 1]);
}

int main(void) {
  UNITY_BEGIN();
  
  RUN_TEST(test_checksum_calculation);
  RUN_TEST(test_command_encoding_mouse_move_abs);
  RUN_TEST(test_command_encoding_keyboard_text);
  RUN_TEST(test_event_encoding_mouse_move);
  RUN_TEST(test_command_encoding_start_recording);
  RUN_TEST(test_command_encoding_status_query);
  
  return UNITY_END();
}
