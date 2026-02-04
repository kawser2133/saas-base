import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgChartsModule } from 'ng2-charts';

@NgModule({
  imports: [CommonModule, NgChartsModule],
  exports: [NgChartsModule]
})
export class ChartsModule { }
