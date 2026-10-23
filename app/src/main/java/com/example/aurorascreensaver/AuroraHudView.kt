package com.example.aurorascreensaver

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.graphics.Color
import android.graphics.Typeface
import android.os.BatteryManager
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.util.TypedValue
import android.view.Gravity
import android.widget.LinearLayout
import android.widget.TextView
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import kotlin.math.roundToInt

class AuroraHudView(context: Context) : LinearLayout(context) {

    private val timeView = TextView(context)
    private val batteryView = TextView(context)

    private val handler = Handler(Looper.getMainLooper())

    private var batteryReceiver: BroadcastReceiver? = null
    private var batteryRegistered = false

    private val updateRunnable = object : Runnable {
        override fun run() {
            updateTime()
            handler.postDelayed(this, 10_000L)
        }
    }

    init {
        orientation = VERTICAL
        gravity = Gravity.TOP or Gravity.START

        isClickable = false
        isFocusable = false

        timeView.setTextColor(Color.argb(230, 228, 240, 255))
        batteryView.setTextColor(Color.argb(190, 168, 220, 255))

        timeView.typeface = Typeface.create("sans-serif-thin", Typeface.NORMAL)
        batteryView.typeface = Typeface.create("sans-serif-light", Typeface.NORMAL)

        timeView.letterSpacing = 0.10f
        batteryView.letterSpacing = 0.18f

        timeView.setShadowLayer(
            18f,
            0f,
            0f,
            Color.argb(90, 110, 200, 255)
        )

        batteryView.setShadowLayer(
            12f,
            0f,
            0f,
            Color.argb(70, 110, 200, 255)
        )

        timeView.text = "--:--"
        batteryView.text = "--%"

        addView(
            timeView,
            LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT
            )
        )

        addView(
            batteryView,
            LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT
            )
        )

        updateTime()
    }

    override fun onAttachedToWindow() {
        super.onAttachedToWindow()
        startUpdates()
        registerBattery()
    }

    override fun onDetachedFromWindow() {
        stopUpdates()
        unregisterBattery()
        super.onDetachedFromWindow()
    }

    override fun onSizeChanged(w: Int, h: Int, oldw: Int, oldh: Int) {
        super.onSizeChanged(w, h, oldw, oldh)

        if (w <= 0 || h <= 0) {
            return
        }

        val minSide = if (w < h) w else h

        val timeSize = minSide * 0.055f
        val batterySize = minSide * 0.021f

        val padX = minSide * 0.045f
        val padY = minSide * 0.040f

        setPadding(
            padX.toInt(),
            padY.toInt(),
            padX.toInt(),
            padY.toInt()
        )

        timeView.setTextSize(
            TypedValue.COMPLEX_UNIT_PX,
            timeSize
        )

        batteryView.setTextSize(
            TypedValue.COMPLEX_UNIT_PX,
            batterySize
        )

        val batteryParams = batteryView.layoutParams as LinearLayout.LayoutParams
        batteryParams.topMargin = (minSide * 0.008f).toInt()
        batteryView.layoutParams = batteryParams
    }

    private fun startUpdates() {
        handler.removeCallbacks(updateRunnable)
        updateTime()
        handler.post(updateRunnable)
    }

    private fun stopUpdates() {
        handler.removeCallbacks(updateRunnable)
    }

    private fun updateTime() {
        val formatter = SimpleDateFormat("HH:mm", Locale.getDefault())
        timeView.text = formatter.format(Date())
    }

    private fun registerBattery() {
        if (batteryRegistered) {
            return
        }

        val filter = IntentFilter(Intent.ACTION_BATTERY_CHANGED)

        val receiver = object : BroadcastReceiver() {
            override fun onReceive(context: Context?, intent: Intent?) {
                updateBattery(intent)
            }
        }

        batteryReceiver = receiver

        try {
            val stickyIntent = if (Build.VERSION.SDK_INT >= 33) {
                context.registerReceiver(
                    receiver,
                    filter,
                    Context.RECEIVER_NOT_EXPORTED
                )
            } else {
                context.registerReceiver(receiver, filter)
            }

            batteryRegistered = true
            updateBattery(stickyIntent)
        } catch (_: Exception) {
            batteryRegistered = false
        }
    }

    private fun unregisterBattery() {
        if (!batteryRegistered) {
            return
        }

        try {
            context.unregisterReceiver(batteryReceiver)
        } catch (_: Exception) {
        }

        batteryRegistered = false
    }

    private fun updateBattery(intent: Intent?) {
        if (intent == null) {
            return
        }

        val level = intent.getIntExtra(BatteryManager.EXTRA_LEVEL, -1)
        val scale = intent.getIntExtra(BatteryManager.EXTRA_SCALE, -1)

        if (level < 0 || scale <= 0) {
            batteryView.text = "--%"
            return
        }

        val percent = ((level.toFloat() / scale.toFloat()) * 100f).roundToInt()

        val status = intent.getIntExtra(BatteryManager.EXTRA_STATUS, -1)

        val charging = status == BatteryManager.BATTERY_STATUS_CHARGING ||
            status == BatteryManager.BATTERY_STATUS_FULL

        batteryView.text = if (charging) {
            "⚡ $percent%"
        } else {
            "$percent%"
        }
    }
}
