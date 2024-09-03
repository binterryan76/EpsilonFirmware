#![no_std]
#![no_main]

use defmt::*;
use embassy_executor::Spawner;
use embassy_stm32::gpio::{AnyPin, Level, Output, Pin, Speed};
use embassy_time::Timer;
use {defmt_rtt as _, panic_probe as _};

#[embassy_executor::task]
async fn spin_motor(step_pin: AnyPin, dir_pin: AnyPin, period: u64){
    //let mut led = Output::new(pin, Level::Low, OutputDrive::Standard);

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
    //let mut led = Output::new(pin, Level::Low, OutputDrive::Standard);

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

#[embassy_executor::main]
async fn main(spawner: Spawner) {
    let p = embassy_stm32::init(Default::default());
    
    //let mut step_pin = Output::new(p.PA7, Level::High, Speed::VeryHigh);
    //let mut dir_pin = Output::new(p.PA6, Level::High, Speed::Low);

    info!("Hello World!");

    

    spawner.spawn(spin_motor(p.PA7.degrade(), p.PA6.degrade(), 5)).unwrap();
    spawner.spawn(spin_motor2(p.PC14.degrade(), p.PC13.degrade(), 10)).unwrap();

    let mut led = Output::new(p.PC15, Level::High, Speed::Low);
    //spawner.spawn(blink_fast(p.PC15.degrade())).unwrap();

    loop {
        //info!("high");
        led.set_high();
        Timer::after_millis(300).await;

        //info!("low");
        led.set_low();
        Timer::after_millis(300).await;
    }
}
