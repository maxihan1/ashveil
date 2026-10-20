// AltTester 서버에 연결해 QA 빌드의 로그인 화면·오브젝트 트리·로그인 실패 안내를 확인하는 JUnit5 스모크 테스트
package com.nimbusgames.ashveil.qa;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.alttester.AltDriver;
import com.alttester.AltObject;
import com.alttester.Commands.FindObject.AltFindObjectsParams;
import com.alttester.Commands.FindObject.AltWaitForObjectsParams;
import com.alttester.Commands.ObjectCommand.AltSetTextParams;
import java.util.Arrays;
import org.junit.jupiter.api.AfterAll;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;

class LoginScreenSmokeTest {
    private static final String HOST = System.getProperty("alt.host", "127.0.0.1");
    private static final int PORT = Integer.parseInt(System.getProperty("alt.port", "13000"));
    private static final String APP = System.getProperty("alt.app", "ashveil");

    private static AltDriver driver;

    @BeforeAll
    static void connect() {
        driver = new AltDriver(HOST, PORT, true, 60, APP);
    }

    @AfterAll
    static void disconnect() throws Exception {
        if (driver != null) driver.stop();
    }

    private static AltObject waitFor(String name) {
        AltFindObjectsParams find = new AltFindObjectsParams.Builder(AltDriver.By.NAME, name).build();
        return driver.waitForObject(new AltWaitForObjectsParams.Builder(find).withTimeout(20).build());
    }

    @Test
    void loginScreenIsShown() {
        assertEquals("Boot", driver.getCurrentScene());
        AltObject login = waitFor("LoginButton");
        assertTrue(login.enabled);
        waitFor("UsernameInput");
        waitFor("PasswordInput");
    }

    @Test
    void objectTreeIsReadable() {
        AltObject[] all = driver.findObjects(new AltFindObjectsParams.Builder(AltDriver.By.PATH, "//*").build());
        assertTrue(all.length > 10, "오브젝트 트리가 비어 있습니다");
        assertTrue(Arrays.stream(all).anyMatch(o -> "LoginView".equals(o.name)));
    }

    @Test
    void wrongPasswordShowsKoreanError() {
        waitFor("UsernameInput").setText(new AltSetTextParams.Builder("nobody_" + System.currentTimeMillis() % 100000).build());
        waitFor("PasswordInput").setText(new AltSetTextParams.Builder("wrong-password").build());
        waitFor("LoginButton").tap();

        AltObject status = waitFor("StatusText");
        String text = "";
        for (int i = 0; i < 20 && (text.isEmpty() || text.contains("중")); i++) {
            sleep(500);
            text = status.getText();
        }
        assertFalse(text.isEmpty(), "로그인 실패 안내가 떠야 합니다");
        assertTrue(text.contains("아이디 또는 비밀번호") || text.contains("서버에 연결"), "실제 문구: " + text);
    }

    private static void sleep(long millis) {
        try {
            Thread.sleep(millis);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }
    }
}
