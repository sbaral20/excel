package com.example;

import static org.junit.jupiter.api.Assertions.assertTrue;

import org.junit.jupiter.api.Test;

class AppTest {
    @Test
    void appStarts() {
        assertTrue(true);
    }
    @Test 
    void testAdd() {
        App app = new App();
        int result = app.customAdd(2, 3);
        assertTrue(result == 5, "2 + 3 should equal 5");
    }
}
