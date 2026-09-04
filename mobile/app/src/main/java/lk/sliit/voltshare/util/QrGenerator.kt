// -----------------------------------------------------------------------------
// File        : QrGenerator.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : Renders the transaction token issued by the Web API as a QR
//               code bitmap.
//
//               Only the drawing happens here. The token itself is created and
//               signed by the service, and checked by the service again when a
//               grid operator scans it, so nothing about its validity is
//               decided on the device.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.util

import android.graphics.Bitmap
import android.graphics.Color
import com.google.zxing.BarcodeFormat
import com.google.zxing.EncodeHintType
import com.google.zxing.WriterException
import com.google.zxing.qrcode.QRCodeWriter
import com.google.zxing.qrcode.decoder.ErrorCorrectionLevel

object QrGenerator {

    /**
     * Draws a token as a square QR code bitmap, or returns null when the token
     * cannot be encoded.
     */
    fun render(token: String, sizePx: Int = 640): Bitmap? {
        if (token.isBlank()) return null

        // A higher error correction level keeps the code readable even when the
        // screen is scratched, dim, or scanned at an angle, which is the normal
        // situation at a station.
        val hints = mapOf(
            EncodeHintType.ERROR_CORRECTION to ErrorCorrectionLevel.H,
            EncodeHintType.MARGIN to 2,
            EncodeHintType.CHARACTER_SET to "UTF-8"
        )

        return try {
            val matrix = QRCodeWriter().encode(token, BarcodeFormat.QR_CODE, sizePx, sizePx, hints)

            val bitmap = Bitmap.createBitmap(sizePx, sizePx, Bitmap.Config.RGB_565)

            // The matrix is copied row by row into an int array and set in one
            // call, which is far quicker than setPixel for each of the
            // hundreds of thousands of pixels involved.
            val pixels = IntArray(sizePx * sizePx)

            for (y in 0 until sizePx) {
                val offset = y * sizePx
                for (x in 0 until sizePx) {
                    pixels[offset + x] = if (matrix.get(x, y)) Color.BLACK else Color.WHITE
                }
            }

            bitmap.setPixels(pixels, 0, sizePx, 0, 0, sizePx, sizePx)
            bitmap
        } catch (failure: WriterException) {
            // An unencodable token is reported as "no image" so the screen can
            // explain the problem rather than crashing.
            null
        }
    }
}
