// -----------------------------------------------------------------------------
// File        : SplashActivity.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Entry point of the application. Reads the session held in the
//               local SQLite database and sends the user straight to the home
//               screen for their role, or to sign in when there is no session.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui

import android.content.Intent
import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import lk.sliit.voltshare.AppServices
import lk.sliit.voltshare.data.remote.ApiConstants
import lk.sliit.voltshare.ui.operator.OperatorHomeActivity
import lk.sliit.voltshare.ui.prosumer.ProsumerHomeActivity

class SplashActivity : AppCompatActivity() {

    /**
     * Decides where to send the user, then finishes so this screen never
     * appears in the back stack.
     */
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // The session is read from SQLite, which is why a user who has signed
        // in once is taken straight to their home screen on later launches.
        val session = AppServices.store.getSession()

        val destination = when {
            session == null -> LoginActivity::class.java

            // Grid operators and prosumers use the same application but are
            // given different home screens, as the specification requires.
            session.role == ApiConstants.ROLE_PROSUMER -> ProsumerHomeActivity::class.java

            else -> OperatorHomeActivity::class.java
        }

        startActivity(Intent(this, destination))
        finish()
    }
}
