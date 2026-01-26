/*
 * Command Parsing Tests
 * Tests for command buffer parsing and validation
 */

#include <unity.h>
#include <string.h>

const int MAX_DATA_LENGTH = 250;
const unsigned long COMMAND_TIMEOUT = 5000;

// Simplified command parsing state
struct CommandBuffer {
  byte buffer[256];
  int position;
  unsigned long lastCommandTime;
  bool isValid;
};

void resetCommandBuffer(CommandBuffer* cmd) {
  cmd->position = 0;
  cmd->lastCommandTime = 0;
  cmd->isValid = false;
}

bool validateCommandType(byte cmdType) {
  return (cmdType == 0x01 || cmdType == 0x02 || cmdType == 0x03 ||
          cmdType == 0x04 || cmdType == 0x05 || cmdType == 0x06 ||
          cmdType == 0x10 || cmdType == 0x11 || cmdType == 0x20);
}

bool parseCommandByte(CommandBuffer* cmd, byte b) {
  if (cmd->position == 0) {
    // Start of new command - validate command type
    if (validateCommandType(b)) {
      cmd->buffer[0] = b;
      cmd->position = 1;
      cmd->lastCommandTime = 1000; // Simulated time
      return true;
    }
    return false; // Invalid command type
  } else if (cmd->position == 1) {
    // Data length low byte
    cmd->buffer[1] = b;
    cmd->position = 2;
    return true;
  } else if (cmd->position == 2) {
    // Data length high byte
    cmd->buffer[2] = b;
    uint16_t dataLength = cmd->buffer[1] | (cmd->buffer[2] << 8);
    
    if (dataLength > MAX_DATA_LENGTH) {
      // Invalid length, reset
      cmd->position = 0;
      return false;
    }
    
    cmd->position = 3;
    cmd->isValid = (dataLength == 0); // Valid if no data needed
    return true;
  }
  
  return false;
}

void setUp(void) {
  // Set up test fixtures
}

void tearDown(void) {
  // Clean up after tests
}

void test_command_buffer_reset() {
  CommandBuffer cmd;
  cmd.position = 5;
  cmd.lastCommandTime = 1000;
  cmd.isValid = true;
  
  resetCommandBuffer(&cmd);
  
  TEST_ASSERT_EQUAL(0, cmd.position);
  TEST_ASSERT_EQUAL(0, cmd.lastCommandTime);
  TEST_ASSERT_FALSE(cmd.isValid);
}

void test_validate_command_type_valid() {
  TEST_ASSERT_TRUE(validateCommandType(0x01));  // MOUSE_MOVE_ABS
  TEST_ASSERT_TRUE(validateCommandType(0x02));  // MOUSE_MOVE_REL
  TEST_ASSERT_TRUE(validateCommandType(0x03));  // MOUSE_CLICK
  TEST_ASSERT_TRUE(validateCommandType(0x04));  // KEYBOARD_TEXT
  TEST_ASSERT_TRUE(validateCommandType(0x05));  // KEY_PRESS
  TEST_ASSERT_TRUE(validateCommandType(0x06));  // DELAY
  TEST_ASSERT_TRUE(validateCommandType(0x10));  // START_RECORDING
  TEST_ASSERT_TRUE(validateCommandType(0x11));  // STOP_RECORDING
  TEST_ASSERT_TRUE(validateCommandType(0x20));  // STATUS_QUERY
}

void test_validate_command_type_invalid() {
  TEST_ASSERT_FALSE(validateCommandType(0x00));
  TEST_ASSERT_FALSE(validateCommandType(0xFF));
  TEST_ASSERT_FALSE(validateCommandType(0x99));
}

void test_parse_command_start() {
  CommandBuffer cmd;
  resetCommandBuffer(&cmd);
  
  bool result = parseCommandByte(&cmd, 0x10); // START_RECORDING
  
  TEST_ASSERT_TRUE(result);
  TEST_ASSERT_EQUAL(1, cmd.position);
  TEST_ASSERT_EQUAL(0x10, cmd.buffer[0]);
}

void test_parse_command_invalid_start() {
  CommandBuffer cmd;
  resetCommandBuffer(&cmd);
  
  bool result = parseCommandByte(&cmd, 0xFF); // Invalid command
  
  TEST_ASSERT_FALSE(result);
  TEST_ASSERT_EQUAL(0, cmd.position);
}

void test_parse_command_length() {
  CommandBuffer cmd;
  resetCommandBuffer(&cmd);
  
  parseCommandByte(&cmd, 0x01); // Command type
  parseCommandByte(&cmd, 0x04); // Length low byte (4)
  parseCommandByte(&cmd, 0x00); // Length high byte
  
  TEST_ASSERT_EQUAL(3, cmd.position);
  uint16_t dataLength = cmd.buffer[1] | (cmd.buffer[2] << 8);
  TEST_ASSERT_EQUAL(4, dataLength);
}

void test_parse_command_length_too_large() {
  CommandBuffer cmd;
  resetCommandBuffer(&cmd);
  
  parseCommandByte(&cmd, 0x01); // Command type
  parseCommandByte(&cmd, 0xFF); // Length low byte (255)
  bool result = parseCommandByte(&cmd, 0x01); // Length high byte (256 total, > MAX_DATA_LENGTH)
  
  TEST_ASSERT_FALSE(result);
  TEST_ASSERT_EQUAL(0, cmd.position); // Should reset
}

void test_parse_command_zero_length() {
  CommandBuffer cmd;
  resetCommandBuffer(&cmd);
  
  parseCommandByte(&cmd, 0x20); // STATUS_QUERY
  parseCommandByte(&cmd, 0x00); // Length low byte (0)
  bool result = parseCommandByte(&cmd, 0x00); // Length high byte (0)
  
  TEST_ASSERT_TRUE(result);
  TEST_ASSERT_TRUE(cmd.isValid); // Should be valid with zero length
}

void test_parse_command_sequence() {
  CommandBuffer cmd;
  resetCommandBuffer(&cmd);
  
  // Simulate parsing a complete command: START_RECORDING (no data)
  TEST_ASSERT_TRUE(parseCommandByte(&cmd, 0x10)); // Command type
  TEST_ASSERT_EQUAL(1, cmd.position);
  
  TEST_ASSERT_TRUE(parseCommandByte(&cmd, 0x00)); // Length low
  TEST_ASSERT_EQUAL(2, cmd.position);
  
  TEST_ASSERT_TRUE(parseCommandByte(&cmd, 0x00)); // Length high
  TEST_ASSERT_EQUAL(3, cmd.position);
  TEST_ASSERT_TRUE(cmd.isValid);
}

int main(void) {
  UNITY_BEGIN();
  
  RUN_TEST(test_command_buffer_reset);
  RUN_TEST(test_validate_command_type_valid);
  RUN_TEST(test_validate_command_type_invalid);
  RUN_TEST(test_parse_command_start);
  RUN_TEST(test_parse_command_invalid_start);
  RUN_TEST(test_parse_command_length);
  RUN_TEST(test_parse_command_length_too_large);
  RUN_TEST(test_parse_command_zero_length);
  RUN_TEST(test_parse_command_sequence);
  
  return UNITY_END();
}
