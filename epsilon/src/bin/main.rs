#![no_std]
#![no_main]

use defmt::*;
use embassy_executor::Spawner;
use embassy_executor::{Executor, InterruptExecutor};
use embassy_stm32::gpio::{AnyPin, Level, Output, Pin, Speed};
use embassy_time::Timer;
use embassy_stm32::time::hz;
use embassy_stm32::{bind_interrupts, peripherals};
use embassy_stm32::{interrupt, pac, Config};
use embassy_stm32::timer::low_level::{Timer as LLTimer, *};
use embassy_stm32::timer::Channel;
use {defmt_rtt as _, panic_probe as _};
use core::mem;
use static_cell::StaticCell;

#[embassy_executor::task]
async fn spin_motor(step_pin: AnyPin, dir_pin: AnyPin, period: u64){
    let mut step = Output::new(step_pin, Level::High, Speed::VeryHigh);
    let mut dir = Output::new(dir_pin, Level::High, Speed::Low);
    dir.set_high();

    let mut steps: u32 = 0;
    loop {
        // Timekeeping is globally available, no need to mess with hardware timers.
        step.set_high();
        Timer::after_millis(period).await;
        step.set_low();
        Timer::after_millis(period).await;
        steps += 1;

        // change directions after every rotation
        if steps == 3200 {
            steps = 0;
            dir.toggle();
        }
    }
}

#[embassy_executor::task]
async fn spin_motor2(step_pin: AnyPin, dir_pin: AnyPin, period: u64){
    let mut step = Output::new(step_pin, Level::High, Speed::VeryHigh);
    let mut dir = Output::new(dir_pin, Level::High, Speed::Low);
    dir.set_high();

    let mut steps: u32 = 0;
    loop {
        // Timekeeping is globally available, no need to mess with hardware timers.
        step.set_high();
        Timer::after_millis(period).await;
        step.set_low();
        Timer::after_millis(period).await;
        steps += 1;

        // change directions after every rotation
        if steps == 3200 {
            steps = 0;
            dir.toggle();
        }
    }
}

static mut IS_HIGH: bool = false;

// Important, when embassy tasks run, they must disable interrupts because even though this is a high priority interrupt, step pulses are still missed
#[interrupt]
fn TIM2() {
    unsafe {
        if IS_HIGH {
            // set pin low
            pac::GPIOA.bsrr().write(|w| w.set_br(7, true));
            IS_HIGH = false;
        }
        else {
            // set pin high
            pac::GPIOA.bsrr().write(|w| w.set_bs(7, true));
            IS_HIGH = true;
        }

        // reset interrupt flag
        pac::TIM2.sr().modify(|r| r.set_uif(false));
    }
}

#[embassy_executor::main]
async fn main(spawner: Spawner) {
    let p = embassy_stm32::init(Default::default());
    let mut peripherals: cortex_m::peripheral::Peripherals = cortex_m::peripheral::Peripherals::take().unwrap();
    
    // set step pin as output so interrupt code works
    let step1 = Output::new(p.PA7, Level::Low, Speed::VeryHigh);

    // initialize timer
    // we cannot use SimplePWM here because the Time is privately encapsulated
    let timer = LLTimer::new(p.TIM2);

    // set counting mode
    timer.set_counting_mode(CountingMode::EdgeAlignedUp);

    // set pwm sample frequency
    timer.set_frequency(hz(125000));

    // enable outputs
    timer.enable_outputs();

    // start timer
    timer.start();

    // set output compare mode
    timer.set_output_compare_mode(Channel::Ch3, OutputCompareMode::PwmMode1);

    // set output compare preload
    timer.set_output_compare_preload(Channel::Ch3, true);

    // set output polarity
    timer.set_output_polarity(Channel::Ch3, OutputPolarity::ActiveHigh);

    // set compare value
    timer.set_compare_value(Channel::Ch3, timer.get_max_compare_value() / 2);

    // enable pwm channel
    timer.enable_channel(Channel::Ch3, true);

    // enable timer interrupts
    timer.enable_update_interrupt(true);

    unsafe {
        peripherals.NVIC.set_priority(interrupt::TIM2, 0); // Set TIM2 priority to 0 (highest)
        cortex_m::peripheral::NVIC::unmask(interrupt::TIM2); // Enable TIM2 interrupt
    };

    //spawner.spawn(spin_motor(p.PA7.degrade(), p.PA6.degrade(), 1)).unwrap();
    spawner.spawn(spin_motor2(p.PC14.degrade(), p.PC13.degrade(), 20)).unwrap();

    let mut led = Output::new(p.PC15, Level::High, Speed::Low);

    loop {
        led.set_high();
        Timer::after_millis(300).await;

        led.set_low();
        Timer::after_millis(300).await;
    }
}
