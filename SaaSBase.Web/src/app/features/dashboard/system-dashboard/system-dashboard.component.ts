import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { ChartConfiguration, ChartOptions, Chart, registerables } from 'chart.js';
import { ChartsModule } from '../../../shared/modules/charts.module';

// Register Chart.js components
Chart.register(...registerables);

@Component({
  selector: 'app-system-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, ChartsModule],
  templateUrl: './system-dashboard.component.html',
  styleUrl: './system-dashboard.component.scss'
})
export class SystemDashboardComponent implements OnInit {
  private readonly api = `${environment.apiBaseUrl}/api/${environment.apiVersion}`;
  
  stats = {
    totalOrganizations: 0,
    totalUsers: 0,
    activeSessions: 0,
    activeOrganizations: 0
  };

  organizations: any[] = [];
  loading = true;

  // Organization Registration Trend Chart
  orgTrendChartData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [{
      data: [],
      label: 'Organizations Registered',
      borderColor: '#667eea',
      backgroundColor: 'rgba(102, 126, 234, 0.1)',
      fill: true,
      tension: 0.4
    }]
  };

  orgTrendChartOptions: ChartOptions<'line'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'top'
      },
      tooltip: {
        mode: 'index',
        intersect: false
      }
    },
    scales: {
      y: {
        beginAtZero: true,
        ticks: {
          stepSize: 1
        }
      }
    }
  };

  // User Growth Trend Chart
  userGrowthChartData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [{
      data: [],
      label: 'Users Registered',
      borderColor: '#f5576c',
      backgroundColor: 'rgba(245, 87, 108, 0.1)',
      fill: true,
      tension: 0.4
    }]
  };

  userGrowthChartOptions: ChartOptions<'line'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'top'
      },
      tooltip: {
        mode: 'index',
        intersect: false
      }
    },
    scales: {
      y: {
        beginAtZero: true,
        ticks: {
          stepSize: 1
        }
      }
    }
  };

  // Organization Active/Inactive Pie Chart
  orgActiveInactiveChartData: ChartConfiguration<'pie'>['data'] = {
    labels: ['Active Organizations', 'Inactive Organizations'],
    datasets: [{
      data: [0, 0],
      backgroundColor: ['#667eea', '#e2e8f0'],
      borderWidth: 0
    }]
  };

  orgActiveInactiveChartOptions: ChartOptions<'pie'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'bottom',
        labels: {
          generateLabels: (chart) => {
            const data = chart.data;
            if (data.labels && data.datasets) {
              const backgroundColor = data.datasets[0].backgroundColor as string[] | undefined;
              return data.labels.map((label, i) => {
                const value = data.datasets[0].data[i] as number;
                const total = (data.datasets[0].data as number[]).reduce((a, b) => a + b, 0);
                const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : '0';
                const color = backgroundColor && Array.isArray(backgroundColor) ? backgroundColor[i] : '#667eea';
                return {
                  text: `${label}: ${value} (${percentage}%)`,
                  fillStyle: color,
                  strokeStyle: color,
                  lineWidth: 0,
                  hidden: false,
                  index: i
                };
              });
            }
            return [];
          }
        }
      },
      tooltip: {
        callbacks: {
          label: (context) => {
            const label = context.label || '';
            const value = context.parsed || 0;
            const total = context.dataset.data.reduce((a: number, b: number) => a + b, 0);
            const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : '0';
            return `${label}: ${value} (${percentage}%)`;
          }
        }
      }
    }
  };

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.loadSystemStats();
    this.loadTrends();
    this.loadActiveInactiveStats();
  }

  loadSystemStats(): void {
    this.http.get<any>(`${this.api}/system/stats`).subscribe({
      next: (data) => {
        this.stats = {
          totalOrganizations: data.totalOrganizations || 0,
          totalUsers: data.totalUsers || 0,
          activeSessions: data.activeSessions || 0,
          activeOrganizations: data.activeOrganizations || 0
        };
        
        // Update organization pie chart with stats data
        const totalOrgs = this.stats.totalOrganizations;
        const activeOrgs = this.stats.activeOrganizations;
        const inactiveOrgs = Math.max(0, totalOrgs - activeOrgs);
        
        this.orgActiveInactiveChartData = {
          labels: ['Active Organizations', 'Inactive Organizations'],
          datasets: [{
            data: [activeOrgs, inactiveOrgs],
            backgroundColor: ['#667eea', '#e2e8f0'],
            borderWidth: 0
          }]
        };
        
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading system stats:', error);
        this.loading = false;
      }
    });
  }

  loadOrganizations(): void {
    this.http.get<any[]>(`${this.api}/system/organizations`).subscribe({
      next: (data) => {
        this.organizations = data || [];
      },
      error: (error) => {
        console.error('Error loading organizations:', error);
      }
    });
  }

  loadTrends(): void {
    // Load organization registration trend
    this.http.get<any>(`${this.api}/system/trends/organizations?months=12`).subscribe({
      next: (data) => {
        this.orgTrendChartData = {
          labels: data.labels || [],
          datasets: [{
            data: data.values || [],
            label: 'Organizations Registered',
            borderColor: '#667eea',
            backgroundColor: 'rgba(102, 126, 234, 0.1)',
            fill: true,
            tension: 0.4
          }]
        };
      },
      error: (error) => {
        console.error('Error loading organization trend:', error);
      }
    });

    // Load user growth trend
    this.http.get<any>(`${this.api}/system/trends/users?months=12`).subscribe({
      next: (data) => {
        this.userGrowthChartData = {
          labels: data.labels || [],
          datasets: [{
            data: data.values || [],
            label: 'Users Registered',
            borderColor: '#f5576c',
            backgroundColor: 'rgba(245, 87, 108, 0.1)',
            fill: true,
            tension: 0.4
          }]
        };
      },
      error: (error) => {
        console.error('Error loading user growth trend:', error);
      }
    });
  }

  loadActiveInactiveStats(): void {
    // Load organization active/inactive stats
    this.http.get<any>(`${this.api}/system/stats/organizations/active-inactive`).subscribe({
      next: (data) => {
        this.orgActiveInactiveChartData = {
          labels: ['Active Organizations', 'Inactive Organizations'],
          datasets: [{
            data: [data.active || 0, data.inactive || 0],
            backgroundColor: ['#667eea', '#e2e8f0'],
            borderWidth: 0
          }]
        };
      },
      error: (error) => {
        console.error('Error loading organization active/inactive stats:', error);
      }
    });
  }
}
