// -----------------------------------------------------------------------------
// File        : app/build.gradle.kts
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Build configuration for the Android application. Reads the
//               Google Maps key from local.properties so it is never committed,
//               and exposes the Web API address as a build constant.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import java.io.FileInputStream
import java.util.Properties

plugins {
    // Kotlin support is built into the Android Gradle plugin from version 9, so
    // no separate Kotlin plugin is declared.
    alias(libs.plugins.android.application)
}

// Load the developer specific values. local.properties is excluded from Git,
// which keeps the Maps key out of the repository while still letting the build
// inject it into the manifest.
val localProperties = Properties()
val localPropertiesFile = rootProject.file("local.properties")
if (localPropertiesFile.exists()) {
    FileInputStream(localPropertiesFile).use { localProperties.load(it) }
}
val mapsApiKey: String = localProperties.getProperty("MAPS_API_KEY") ?: ""

android {
    namespace = "lk.sliit.voltshare"
    compileSdk {
        version = release(37)
    }

    defaultConfig {
        applicationId = "lk.sliit.voltshare"
        minSdk = 24
        targetSdk = 37
        versionCode = 1
        versionName = "1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"

        // Substituted into AndroidManifest.xml at build time.
        manifestPlaceholders["MAPS_API_KEY"] = mapsApiKey

        // Address of the VoltShare Web API.
        //
        // 10.0.2.2 is the alias the Android emulator uses to reach the machine
        // it is running on; inside the emulator, "localhost" would mean the
        // emulator itself. Change this to the machine's network address, for
        // example http://192.168.1.190:8080/api/v1/, to run on a real handset.
        buildConfigField("String", "API_BASE_URL", "\"http://192.168.1.190:8080/api/v1/\"")
    }

    buildFeatures {
        // View binding gives type safe access to the views in each layout,
        // which removes every findViewById call and the casting mistakes that
        // come with them.
        viewBinding = true

        // Required for the buildConfigField values declared above.
        buildConfig = true
    }

    buildTypes {
        release {
            optimization {
                enable = false
            }
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_11
        targetCompatibility = JavaVersion.VERSION_11
    }
}

dependencies {
    // Core Android and Material Design components.
    implementation(libs.androidx.activity.ktx)
    implementation(libs.androidx.appcompat)
    implementation(libs.androidx.constraintlayout)
    implementation(libs.androidx.core.ktx)
    implementation(libs.material)

    // Lists and pull to refresh.
    implementation(libs.androidx.recyclerview)
    implementation(libs.androidx.swiperefreshlayout)

    // Coroutines, so network calls run off the main thread and are cancelled
    // automatically when the screen that started them is destroyed.
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.kotlinx.coroutines.android)

    // REST access to the central Web API.
    implementation(libs.retrofit)
    implementation(libs.retrofit.gson)
    implementation(libs.okhttp.logging)

    // Google Maps and device location.
    implementation(libs.play.services.maps)
    implementation(libs.play.services.location)

    // QR code generation and scanning.
    implementation(libs.zxing.embedded)
    implementation(libs.zxing.core)

    testImplementation(libs.junit)
    androidTestImplementation(libs.androidx.espresso.core)
    androidTestImplementation(libs.androidx.junit)
}
