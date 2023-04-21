
;.MODEL FLAT, STDCALL

OPTION CASEMAP:NONE
;INCLUDE C:\masm32\include\windows.inc

; na windowse bêdzie to rcx 

proc_args_register equ rcx

	; struct gaussian_blur_args
	; {
	;     uint8_t* input_pixels;
	;     uint8_t* output_pixels;

	;     uint32_t radius;

	;     float* kernel;

	;     uint32_t input_width;
	;     uint32_t input_height;
	; };

input_pixels_off 	equ 0
output_pixels_off	equ 8

radius_off			equ 16

kernel_off			equ 24

input_width_off		equ 32
input_height_off	equ 40

.CODE

gaussian_blur PROC

	; wrzucamy wartoœæ rejestru rbp na stos i nadpisujemy wartoœæi¹ rejestru rsp

	push rbp
	mov rbp, rsp

	; zgodnie z konwencj¹ wywo³ania musimy zapisaæ wartoœci rejestrów RBX, RDI, RSI, R12, R13, R14, R15; rejestrów XMM6-XMM15 nie u¿ywamy

	push rdi
	push rsi
	push r12
	push r13
	push r14
	push r15

	; w rejestrze r15 bêdziemy przechowywali wskaŸnik do struktury z argumentami

	mov r15, proc_args_register

	; jeœli input_width < (2*radius+1) lub input_height < (2*radius+1), opuszczamy procedurê, zg³aszaj¹c b³¹d

	mov r14, [r15 + radius_off]

	add r14, r14
	add r14, 1

	mov r13, [r15 + input_width_off]

	cmp r13, r14 
	jl proc_end_error

	mov r13, [r15 + input_height_off]

	cmp r13, r14 
	jl proc_end_error

	; rejestr r14 bêdzie przechowywa³ zmienn¹, s³u¿¹c¹ do iterowania po wysokoœci obrazu (omijamy $radius pierwszych i ostatnich pikseli)
	; r14 : [radius, height - radius)

	mov r14, [r15 + radius_off]
	
	loop_image_height:

		; sprawdzamy, czy r14 == height-radius

		mov r12, [r15 + input_height_off]
		mov r11, [r15 + radius_off]

		sub r12, r11

		cmp r14, r12
		jz loop_image_height_end

		; rejestr r13 bêdzie przechowywa³ zmienn¹, s³u¿¹c¹ do iterowania po szerokoœci obrazu (omijamy $radius pierwszych i ostatnich pikseli)
		; r13 : [radius, width - radius)

		mov r13, [r15 + radius_off]

		loop_image_width:

			; sprawdzamy, czy r13 == width-radius

			mov r12, [r15 + input_width_off]
			mov r11, [r15 + radius_off]

			sub r12, r11

			cmp r13, r12 
			jz loop_image_width_end

			; rejestr xmm2 bêdzie przechowywa³ sumy iloczynów elelmentów kernela i sk³adowych
			; w tym celu zerujemy go na pocz¹tek

			xorps xmm2, xmm2

			; rejestr r11 bêdzie przechowywa³ zmienn¹, s³u¿¹c¹ do iterowania po wysokoœci kernela,
			; r11: [0, 2*radius + 1)

			mov r11, 0

			loop_kernel_height:

				; sprawdzamy, czy r11 == 2*radius + 1

				mov r10, [r15 + radius_off]
				
				add r10, r10
				add r10, 1

				cmp r11, r10
				jz loop_kernel_height_end				

				; rejestr r10 bêdzie przechowywa³ zmienn¹, s³u¿¹c¹ do iterowania po szerokoœci kernela,
				; r10: [0, 2*radius + 1)

				mov r10, 0

				loop_kernel_width:

					; sprawdzamy, czy r10 == 2*radius + 1

					mov r9, [r15 + radius_off]
					
					add r9, r9
					add r9, 1

					cmp r10, r9
					jz loop_kernel_width_end
					
					; pobieramy adres piksela na pozycji (r14+r11-radius, r13+r10-radius)

					mov rax, r14
					add rax, r11

					mov r9, [r15 + radius_off]
					sub rax, r9

					mov r9, [r15 + input_width_off]
					mul r9

					add rax, r13
					add rax, r10

					mov r9, [r15 + radius_off]
					sub rax, r9

					; mno¿ymy adres piksela przez 3 (piksel sk³ada siê z 3 bajtów)

					mov r9, 3
					mul r9

					mov r9, [r15 + input_pixels_off]
					add rax, r9

					; rejestr r9 przechowuje adres piksela w obrazie

					mov r9, rax

					; pobieramy wartoœci elementu kernela na pozycji (r11, r10)

					mov rax, r11

					; (r11)*(2*radius+1)

					mov r8, [r15 + radius_off]

					add r8, r8
					add r8, 1

					mul r8

					; (r11)*(2*radius+1) + r10

					add rax, r10

					; mno¿ymy pozycjê elementu kernela przez 4 (ma on rozmiar 4 bajtowego float'a)

					mov r8, 4
					mul r8

					mov r8, [r15 + kernel_off]
					add rax, r8

					; pobieramy wartoœæ elementu kernela do 4 elementów rejestru xmm1

					VBROADCASTSS xmm1, dword ptr [rax]

					; pobieramy wartoœci kolejnych sk³adowych piksela, i zapisujemy je
					; w rejestrze xmm0, za ka¿dym razem przesuwaj¹c poprzednie sk³adowe na starsze pozycje w wektorze

					mov rax, 0 ; zerujemy górne bajty rejestru rax

					mov al, [r9]
					cvtsi2ss xmm0, rax				; konwertowanie zmiennej ca³kowitej na zmiennoprzecinkow¹ w pierwszym elemencie xmm0
					shufps xmm0, xmm0, 10010011b	; przesuwanie elementów xmm0 na starsze pozycje
					inc r9

					mov al, [r9]
					cvtsi2ss xmm0, rax				
					shufps xmm0, xmm0, 10010011b
					inc r9

					mov al, [r9]
					cvtsi2ss xmm0, rax

					; zawartoœæ rejestru r9 nie jest nam ju¿ potrzebna

					; mno¿ymy poszczególne elementy xmm0 i xmm1

					mulps xmm0, xmm1

					; dodajemy wartoœæi iloczynu dla kolejnych sk³adowych piksela, do elementów rejestru xmm2

					addps xmm2, xmm0

					inc r10
					jmp loop_kernel_width

				loop_kernel_width_end:

				;

				inc r11
				jmp loop_kernel_height

			loop_kernel_height_end:

			; okreœlamy adres piksela w wyjœciowym obrazie na pozycji (r14-radius, r13-radius)

			; (r14-radius)*(width-2*radius)+r13-radius

			mov rax, r14

			mov r9, [r15 + radius_off]
			sub rax, r9

			mov r8, [r15 + input_width_off]
			sub r8, r9
			sub r8, r9

			mul r8

			add rax, r13
			sub rax, r9

			; mno¿ymy adres piksela przez 3

			mov r9, 3
			mul r9

			mov r9, [r15 + output_pixels_off]
			add rax, r9

			; rejestr r9 przechowuje teraz adres wyjœciowego piksela

			mov r9, rax

			cvtss2si rax, xmm2
			mov [r9+2], al
			shufps xmm2, xmm2, 00111001b ; przesuwanie elementów rejestru xmm0 na m³odsze pozycje


			cvtss2si rax, xmm2
			mov [r9+1], al
			shufps xmm2, xmm2, 00111001b 

			cvtss2si rax, xmm2
			mov [r9], al

			;

			inc r13
			jmp loop_image_width

		loop_image_width_end:	

		inc r14
		jmp loop_image_height

	loop_image_height_end:

	proc_end_success:

		mov rax, 0
		jmp proc_end

	proc_end_error:

		mov rax, 1

	proc_end:

		; przywracamy wejœciowe wartoœci resjtrów

		;pop proc_args_register

		pop r15
		pop r14
		pop r13
		pop r12
		pop rsi
		pop rdi

		pop rbp

		ret

gaussian_blur ENDP

END
