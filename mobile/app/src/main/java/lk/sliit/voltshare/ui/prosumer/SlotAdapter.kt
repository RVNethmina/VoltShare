// -----------------------------------------------------------------------------
// File        : SlotAdapter.kt
// Project     : VoltShare Mobile - Smart Solar Microgrid Trading System
// Description : RecyclerView adapter for the bookable window picker. Marks the
//               chosen window and refuses selection of a full one, using the
//               remaining capacity the Web API reported.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

package lk.sliit.voltshare.ui.prosumer

import android.view.LayoutInflater
import android.view.ViewGroup
import androidx.core.content.ContextCompat
import androidx.recyclerview.widget.RecyclerView
import lk.sliit.voltshare.R
import lk.sliit.voltshare.data.remote.SlotDto
import lk.sliit.voltshare.databinding.ItemSlotBinding
import lk.sliit.voltshare.util.Formatters

class SlotAdapter(
    private val onSelected: (SlotDto) -> Unit
) : RecyclerView.Adapter<SlotAdapter.SlotViewHolder>() {

    private val slots = mutableListOf<SlotDto>()

    /** Identifier of the window the user has chosen, if any. */
    var selectedSlotId: String? = null
        private set

    /**
     * Replaces the list, keeping the current selection only if that window is
     * still present in the new list.
     */
    fun submit(newSlots: List<SlotDto>) {
        slots.clear()
        slots.addAll(newSlots)

        if (slots.none { it.id == selectedSlotId }) {
            selectedSlotId = null
        }

        notifyDataSetChanged()
    }

    /** Pre-selects a window, used when an existing booking is being changed. */
    fun preselect(slotId: String?) {
        selectedSlotId = slotId
        notifyDataSetChanged()
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): SlotViewHolder {
        val binding = ItemSlotBinding.inflate(
            LayoutInflater.from(parent.context), parent, false
        )
        return SlotViewHolder(binding)
    }

    override fun onBindViewHolder(holder: SlotViewHolder, position: Int) {
        holder.bind(slots[position])
    }

    override fun getItemCount(): Int = slots.size

    inner class SlotViewHolder(
        private val binding: ItemSlotBinding
    ) : RecyclerView.ViewHolder(binding.root) {

        /**
         * Fills one window card and handles its selection.
         */
        fun bind(slot: SlotDto) {
            val context = binding.root.context

            binding.textWindow.text = Formatters.window(slot.startTimeUtc, slot.endTimeUtc)
            binding.textEnergy.text = "${Formatters.energy(slot.energyKwhPerSlot)} per place"

            val isFull = slot.remainingCapacity <= 0

            if (isFull) {
                binding.textRemaining.text = context.getString(R.string.slot_full)
                binding.textRemaining.setBackgroundColor(
                    ContextCompat.getColor(context, R.color.status_rejected_bg)
                )
                binding.textRemaining.setTextColor(
                    ContextCompat.getColor(context, R.color.status_rejected_fg)
                )
            } else {
                binding.textRemaining.text =
                    context.getString(R.string.slot_free_format, slot.remainingCapacity)
                binding.textRemaining.setBackgroundColor(
                    ContextCompat.getColor(context, R.color.status_completed_bg)
                )
                binding.textRemaining.setTextColor(
                    ContextCompat.getColor(context, R.color.status_completed_fg)
                )
            }

            // A full window is dimmed and cannot be chosen. The service refuses
            // it as well, so this only saves the user a pointless request.
            binding.cardSlot.isEnabled = !isFull
            binding.root.alpha = if (isFull) 0.5f else 1f

            val isSelected = slot.id == selectedSlotId
            binding.cardSlot.strokeColor = ContextCompat.getColor(
                context,
                if (isSelected) R.color.brand_500 else R.color.ink_200
            )

            binding.root.setOnClickListener {
                if (isFull) return@setOnClickListener

                selectedSlotId = slot.id
                notifyDataSetChanged()
                onSelected(slot)
            }
        }
    }
}
