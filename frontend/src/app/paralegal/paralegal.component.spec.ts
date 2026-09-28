import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ParalegalComponent } from './paralegal.component';

describe('ParalegalComponent', () => {
  let component: ParalegalComponent;
  let fixture: ComponentFixture<ParalegalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ParalegalComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ParalegalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
